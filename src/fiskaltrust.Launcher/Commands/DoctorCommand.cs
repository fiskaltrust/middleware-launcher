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

    public class DoctorCommandHandler : CommonCommandHandler
    {
        private readonly ILifetime _lifetime;
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public DoctorCommandHandler(ILifetime lifetime, LauncherExecutablePath launcherExecutablePath)
        {
            _lifetime = lifetime;
            _launcherExecutablePath = launcherExecutablePath;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            if (File.Exists(LauncherConfigurationFile))
            {
                _launcherConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(LauncherConfigurationFile));
            }

            if (MergeLegacyConfigIfExists && File.Exists(LegacyConfigurationFile))
            {
                var legacyConfig = await LegacyConfigFileReader.ReadLegacyConfigFile(LegacyConfigurationFile);
                _launcherConfiguration.OverwriteWith(legacyConfig);
            }

            _launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            _launcherConfiguration.EnableDefaults();

            _clientEcdh = await LoadCurve(_launcherConfiguration.AccessToken!, _launcherConfiguration.UseOffline!.Value, dryRun: true);

            using var downloader = new ConfigurationDownloader(_launcherConfiguration);

            string? cashboxConfiguration = null;

            try
            {
                cashboxConfiguration = await downloader.GetConfigurationAsync(_clientEcdh);
            }
            catch
            {
                Log.Warning("No configuration file downloaded yet");
            }

            if (cashboxConfiguration is not null)
            {
                _launcherConfiguration.OverwriteWith(LauncherConfigurationInCashBoxConfiguration.Deserialize(cashboxConfiguration));

                _cashboxConfiguration = CashBoxConfigurationExt.Deserialize(cashboxConfiguration);
                _cashboxConfiguration.Decrypt(_launcherConfiguration, _clientEcdh);
            }

            _dataProtectionProvider = DataProtectionExtensions.Create(_launcherConfiguration.AccessToken);

            _launcherConfiguration.Decrypt(_dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE));

            var doctorId = Guid.NewGuid();
            var doctorProcessHostMonarch = new DoctorProcessHostMonarch();

            var monarchBuilder = WebApplication.CreateBuilder();
            monarchBuilder.Host
                .UseSerilog(new LoggerConfiguration().CreateLogger())
                .ConfigureServices((_, services) =>
                {
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(_ => _launcherConfiguration);
                    services.AddSingleton(_ => _lifetime);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, IProcessHostMonarch>() {
                        {
                            doctorId,
                            doctorProcessHostMonarch
                        }
                });
                    services.AddSingleton(_ => Log.Logger);
                    services.AddSingleton(_ => _launcherExecutablePath);
                });

            monarchBuilder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrelForGrpc(options, new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}")));

            monarchBuilder.Services.AddCodeFirstGrpc();

            var monarchApp = monarchBuilder.Build();

            monarchApp.UseRouting();
#pragma warning disable ASP0014
            monarchApp.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());
#pragma warning restore ASP0014

            try
            {
                await monarchApp.StartAsync(_lifetime.ApplicationLifetime.ApplicationStopping);
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured.");
                Log.CloseAndFlush();
                return 1;
            }

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

            IProcessHostService? processHostService = null;
            processHostService = GrpcChannel.ForAddress($"http://localhost:{_launcherConfiguration.LauncherPort}").CreateGrpcService<IProcessHostService>();

            var plebianBuilder = Host.CreateDefaultBuilder()
                .UseSerilog(new LoggerConfiguration().CreateLogger())
                .ConfigureServices(services =>
                {
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(_ => _launcherConfiguration);
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
                });

            var plebianApp = plebianBuilder.Build();
            try
            {
                await plebianApp.StartAsync(_lifetime.ApplicationLifetime.ApplicationStopping);
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured.");
                Log.CloseAndFlush();
                return 1;
            }

            await doctorProcessHostMonarch.IsStarted.Task;

            await _lifetime.StopAsync(new CancellationToken());

            await monarchApp.StopAsync();
            await monarchApp.WaitForShutdownAsync();

            await plebianApp.StopAsync();
            await plebianApp.WaitForShutdownAsync();

            Log.Information("Doctor found no issues.");
            return 0;
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
