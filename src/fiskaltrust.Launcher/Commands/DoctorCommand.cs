using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;
using fiskaltrust.Launcher.Services.Interfaces;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.Launcher.AssemblyLoading;
using fiskaltrust.Launcher.Clients;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.ifPOS.v1;

namespace fiskaltrust.Launcher.Commands
{
    public class DoctorCommand : RunCommand
    {
        public DoctorCommand(string name = "doctor") : base(name) { }
    }

    public class DoctorCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;
        public string LegacyConfigurationFile { get; set; } = null!;
        public bool MergeLegacyConfigIfExists { get; set; }

        private const string SUCCESS = "✅";
        private const string ERROR = "❌";
        private const string WARNING = "⚠️";
        private readonly ILifetime _lifetime;
        private readonly LauncherExecutablePath _launcherExecutablePath;
        private bool _failed = false;

        public DoctorCommandHandler(ILifetime lifetime, LauncherExecutablePath launcherExecutablePath)
        {
            _lifetime = lifetime;
            _launcherExecutablePath = launcherExecutablePath;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .AddLoggingConfiguration()
                    .CreateLogger();

                LauncherConfiguration launcherConfiguration = new();

                if (File.Exists(LauncherConfigurationFile))
                {
                    launcherConfiguration = await CheckAwait("Parse launcher configuration", async () => LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(LauncherConfigurationFile))) ?? new LauncherConfiguration();
                }

                if (MergeLegacyConfigIfExists && File.Exists(LegacyConfigurationFile))
                {
                    var legacyConfig = await CheckAwait("Parse legacy configuration file", async () => await LegacyConfigFileReader.ReadLegacyConfigFile(LegacyConfigurationFile));
                    if (legacyConfig is not null)
                    {
                        launcherConfiguration.OverwriteWith(legacyConfig);
                    }
                }

                launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

                launcherConfiguration.EnableDefaults();

                var clientEcdh = await CheckAwait("Load ECDH Curve", async () => await CommonCommandHandler.LoadCurve(launcherConfiguration.AccessToken!, launcherConfiguration.UseOffline!.Value, dryRun: true), critical: false);
                ftCashBoxConfiguration cashboxConfiguration = new();

                if (clientEcdh is null)
                { }
                else
                {
                    using var downloader = new ConfigurationDownloader(launcherConfiguration);

                    string? cashboxConfigurationString = null;

                    cashboxConfigurationString = await CheckAwait("Download cashbox configuration", async () => await downloader.GetConfigurationAsync(clientEcdh));

                    if (cashboxConfigurationString is null)
                    {
                        if (launcherConfiguration.UseOffline!.Value)
                        {
                            Log.Warning("No configuration file downloaded yet");
                        }
                    }
                    else
                    {
                        var launcherConfigurationInCashBoxConfiguration = Check("Parse cashbox configuration in launcher configuration", () => LauncherConfigurationInCashBoxConfiguration.Deserialize(cashboxConfigurationString));
                        if (launcherConfigurationInCashBoxConfiguration is not null)
                        {
                            launcherConfiguration.OverwriteWith(launcherConfigurationInCashBoxConfiguration);
                        }

                        var cashboxConfigurationInner = Check("Parse cashbox configuration", () => CashBoxConfigurationExt.Deserialize(cashboxConfigurationString));
                        if (cashboxConfigurationInner is not null)
                        {
                            Check("Decrypt cashbox configuration", () => cashboxConfigurationInner.Decrypt(launcherConfiguration, clientEcdh));
                            cashboxConfiguration = cashboxConfigurationInner;
                        }
                    }
                }

                var dataProtectionProvider = Check("Setup data protection", () => DataProtectionExtensions.Create(launcherConfiguration.AccessToken));
                if (dataProtectionProvider is not null)
                {
                    Check("Decrypt launcher configuration", () => launcherConfiguration.Decrypt(dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE)));
                }

                var doctorId = Guid.NewGuid();
                var doctorProcessHostMonarch = new DoctorProcessHostMonarch();

                var monarchBuilder = WebApplication.CreateBuilder();
                monarchBuilder.Host
                    .UseSerilog(new LoggerConfiguration().CreateLogger())
                    .ConfigureServices((_, services) =>
                    {
                        Check("Setup monarch services", () =>
                        {
                            services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                            services.AddSingleton(_ => launcherConfiguration);
                            services.AddSingleton(_ => _lifetime);
                            services.AddSingleton(_ => cashboxConfiguration);
                            services.AddSingleton(_ => new Dictionary<Guid, IProcessHostMonarch>() {
                            {
                                doctorId,
                                doctorProcessHostMonarch
                            }
                            });
                            services.AddSingleton(_ => Log.Logger);
                            services.AddSingleton(_ => _launcherExecutablePath);
                        }, throws: true);
                    });

                Check("Setup monarch ProcessHostService", () =>
                {
                    monarchBuilder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrelForGrpc(options, new Uri($"http://[::1]:{launcherConfiguration.LauncherPort}")));

                    monarchBuilder.Services.AddCodeFirstGrpc();
                }, throws: true);

                var monarchApp = Check("Build monarch WebApplication", monarchBuilder.Build, throws: true)!;

                monarchApp.UseRouting();
#pragma warning disable ASP0014
                monarchApp.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());
#pragma warning restore ASP0014

                await CheckAwait("Start monarch WebApplication", async () => await WithTimeout(async () => await monarchApp.StartAsync(_lifetime.ApplicationLifetime.ApplicationStopping), TimeSpan.FromSeconds(5)), throws: true);

                var plebianConfiguration = new PlebianConfiguration
                {
                    PackageId = doctorId,
                    PackageType = Constants.PackageType.Queue
                };

                var packageConfiguration = new PackageConfiguration
                {
                    Configuration = new(),
                    Id = plebianConfiguration.PackageId,
                    Package = "none",
                    Url = Array.Empty<string>(),
                    Version = "1.0.0"
                };

                IProcessHostService? processHostService = Check("Start plebian processhostservice client", () => GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort}").CreateGrpcService<IProcessHostService>());

                var plebianBuilder = Host.CreateDefaultBuilder()
                    .UseSerilog(new LoggerConfiguration().CreateLogger())
                    .ConfigureServices(services =>
                    {
                        Check("Setup plebian services", () =>
                        {
                            services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                            services.AddSingleton(_ => launcherConfiguration);
                            services.AddSingleton(_ => packageConfiguration);
                            services.AddSingleton(_ => plebianConfiguration);

                            var pluginLoader = new PluginLoader();
                            services.AddSingleton(_ => pluginLoader);

                            if (processHostService is not null)
                            {
                                services.AddSingleton(_ => processHostService);
                            }

                            services.AddSingleton<HostingService>();
                            services.AddHostedService<ProcessHostPlebian>();

                            services.AddSingleton<IClientFactory<IDESSCD>, DESSCDClientFactory>();
                            services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();

                            var bootstrapper = new DoctorMiddlewareBootstrapper
                            {
                                Id = packageConfiguration.Id,
                                Configuration = packageConfiguration.Configuration.ToDictionary(c => c.Key, c => (object?)c.Value.ToString())!
                            };

                            bootstrapper.ConfigureServices(services);

                            services.AddSingleton(_ => bootstrapper);
                        }, throws: true);
                    });

                var plebianApp = Check("Build plebian Host", plebianBuilder.Build, throws: true)!;

                await CheckAwait("Start plebian Host", async () => await WithTimeout(async () => await plebianApp.StartAsync(_lifetime.ApplicationLifetime.ApplicationStopping), TimeSpan.FromSeconds(5)));

                await doctorProcessHostMonarch.IsStarted.Task;

                await CheckAwait("Shutdown launcher gracefully", async () => await WithTimeout(async () =>
                    {
                        await _lifetime.StopAsync(new CancellationToken());

                        await monarchApp.StopAsync();
                        await monarchApp.WaitForShutdownAsync();

                        await plebianApp.StopAsync();
                        await plebianApp.WaitForShutdownAsync();
                    }, TimeSpan.FromSeconds(5))
                );
            }
            catch (Exception e)
            {
                _failed = true;
                Log.Error(e, "Doctor found errors.");
            }

            if (_failed)
            {
                return 1;
            }
            else
            {
                Log.Information($"Doctor found no issues.");
                return 0;
            }
        }

        private static async Task WithTimeout(Func<Task> action, TimeSpan timeout)
        {
            var actionTask = action();
            var completed = await Task.WhenAny(new[] {
                Task.Delay(timeout),
                actionTask
            });

            if (completed != actionTask)
            {
                throw new TimeoutException("Exceeded timeout.");
            }
        }

        private async Task<T?> CheckAwait<T>(string operation, Func<Task<T>> action, bool critical = true, bool throws = false)
        {
            try
            {
                T result = await action();
                Log.Information($"{SUCCESS} {operation}");
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e, $"{ERROR} {operation}");
                if (critical)
                {
                    _failed = true;
                }
                if (throws) { throw; }
                return default;
            }
        }

        private async Task CheckAwait(string operation, Func<Task> action, bool critical = true, bool throws = false)
        {
            try
            {
                await action();
                Log.Information($"{SUCCESS} {operation}");
            }
            catch (Exception e)
            {
                Log.Error(e, $"{ERROR} {operation}");
                if (critical)
                {
                    _failed = true;
                }
                if (throws) { throw; }
            }
        }

        private T? Check<T>(string operation, Func<T> action, bool critical = true, bool throws = false)
        {
            try
            {
                T result = action();
                Log.Information($"{SUCCESS} {operation}");
                return result;
            }
            catch (Exception e)
            {
                Log.Error(e, $"{ERROR} {operation}");
                if (critical)
                {
                    _failed = true;
                }
                if (throws) { throw; }
                return default;
            }
        }

        private bool Check(string operation, Action action, bool critical = true, bool throws = false)
        {
            try
            {
                action();
                Log.Information($"{SUCCESS} {operation}");
                return true;
            }
            catch (Exception e)
            {
                if (critical)
                {
                    Log.Error(e, $"{ERROR} {operation}");
                    _failed = true;
                }
                else
                {
                    Log.Warning(e, $"{WARNING} {operation}");
                }
                if (throws) { throw; }
                return false;
            }
        }
    }


    public class DoctorProcessHostMonarch : IProcessHostMonarch
    {
        public TaskCompletionSource IsStarted = new();
        public Task Start(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Started()
        {
            IsStarted.SetResult();
        }

        public Task Stopped()
        {
            return Task.CompletedTask;
        }
    }

    public class DoctorMiddlewareBootstrapper : IMiddlewareBootstrapper
    {
        public required Guid Id { get; set; }
        public required Dictionary<string, object> Configuration { get; set; }

        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IPOS>(new DoctorQueue());
        }
    }

    public class DoctorQueue : IPOS
    {
        public IAsyncResult BeginEcho(string message, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginJournal(long ftJournalType, long from, long to, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult BeginSign(ifPOS.v0.ReceiptRequest data, AsyncCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public string Echo(string message)
        {
            throw new NotImplementedException();
        }

        public Task<EchoResponse> EchoAsync(EchoRequest message)
        {
            throw new NotImplementedException();
        }

        public string EndEcho(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public Stream EndJournal(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public ifPOS.v0.ReceiptResponse EndSign(IAsyncResult result)
        {
            throw new NotImplementedException();
        }

        public Stream Journal(long ftJournalType, long from, long to)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<JournalResponse> JournalAsync(JournalRequest request)
        {
            throw new NotImplementedException();
        }

        public ifPOS.v0.ReceiptResponse Sign(ifPOS.v0.ReceiptRequest data)
        {
            throw new NotImplementedException();
        }

        public Task<ReceiptResponse> SignAsync(ReceiptRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
