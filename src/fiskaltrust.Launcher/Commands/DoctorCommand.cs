using System.CommandLine;
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
using Microsoft.AspNetCore.Server.Kestrel.Core;
using fiskaltrust.Launcher.Factories;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = fiskaltrust.Launcher.Common.Extensions.LoggerExtensions;

namespace fiskaltrust.Launcher.Commands
{
    public class DoctorCommand : RunCommand
    {
        public DoctorCommand(string name = "doctor") : base(name) { }
    }

    public class DoctorOptions
    {
        public DoctorOptions(LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile, string legacyConfigurationFile, bool mergeLegacyConfigIfExists)
        {
            ArgsLauncherConfiguration = argsLauncherConfiguration;
            LauncherConfigurationFile = launcherConfigurationFile;
            LegacyConfigurationFile = legacyConfigurationFile;
        }

        public LauncherConfiguration ArgsLauncherConfiguration { get; set; }
        public string LauncherConfigurationFile { get; set; }
        public string LegacyConfigurationFile { get; set; }
    }

    public class DoctorServices
    {
        public DoctorServices(ILifetime lifetime, LauncherExecutablePath launcherExecutablePath)
        {
            Lifetime = lifetime;
            LauncherExecutablePath = launcherExecutablePath;
        }

        public readonly ILifetime Lifetime;
        public readonly LauncherExecutablePath LauncherExecutablePath;
    }

    public static class DoctorHandler
    {
        public static async Task<int> HandleAsync(CommonOptions commonOptions, CommonProperties commonProperties, DoctorOptions doctorOptions, DoctorServices doctorServices)
        {
            var checkUp = new CheckUp();

            try
            {
                Log.Logger = new LoggerConfiguration()
                    .AddLoggingConfiguration()
                    .CreateLogger();

                var logger = LoggerExtensions.CreateFromSerilog();

                LauncherConfiguration launcherConfiguration = new();

                if (File.Exists(commonOptions.LegacyConfigurationFile))
                {
                    var legacyConfig = await checkUp.CheckAwait("Parse legacy configuration file", async () => await LegacyConfigFileReader.ReadLegacyConfigFile(commonOptions.LegacyConfigurationFile), logger);
                    if (legacyConfig is not null)
                    {
                        launcherConfiguration.OverwriteWith(legacyConfig);
                    }
                }

                if (File.Exists(commonOptions.LauncherConfigurationFile))
                {
                    var config = await checkUp.CheckAwait("Parse launcher configuration", async () => LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(commonOptions.LauncherConfigurationFile)), logger);
                    if (config is not null)
                    {
                        launcherConfiguration.OverwriteWith(config);
                    }
                }

                launcherConfiguration.OverwriteWith(doctorOptions.ArgsLauncherConfiguration);

                var clientEcdh = await checkUp.CheckAwait("Load ECDH Curve", async () => await CommonHandler.LoadCurve(launcherConfiguration, logger, dryRun: true), logger, critical: false);
                ftCashBoxConfiguration cashboxConfiguration = new();

                if (clientEcdh is null)
                {
                    logger.FailedToLoadEcdhCurve();
                }
                else
                {
                    using var downloader = new ConfigurationDownloader(launcherConfiguration);

                    string? cashboxConfigurationString = null;

                    cashboxConfigurationString = await checkUp.CheckAwait("Download cashbox configuration", async () => await downloader.GetConfigurationAsync(clientEcdh), logger);

                    if (cashboxConfigurationString is null)
                    {
                        if (launcherConfiguration.UseOffline!.Value)
                        {
                            logger.NoConfigurationFileDownloadedYet();
                        }
                    }
                    else
                    {
                        var launcherConfigurationInCashBoxConfiguration = checkUp.Check("Parse cashbox configuration in launcher configuration", () => LauncherConfigurationInCashBoxConfiguration.Deserialize(cashboxConfigurationString), logger);
                        if (launcherConfigurationInCashBoxConfiguration is not null)
                        {
                            launcherConfiguration.OverwriteWith(launcherConfigurationInCashBoxConfiguration);
                        }

                        var cashboxConfigurationInner = checkUp.Check("Parse cashbox configuration", () => CashBoxConfigurationExt.Deserialize(cashboxConfigurationString), logger);
                        if (cashboxConfigurationInner is not null)
                        {
                            checkUp.Check("Decrypt cashbox configuration", () => cashboxConfigurationInner.Decrypt(launcherConfiguration, clientEcdh), logger);
                            cashboxConfiguration = cashboxConfigurationInner;
                        }
                    }
                }

                var dataProtectionProvider = checkUp.Check("Setup data protection", () => DataProtectionExtensions.Create(launcherConfiguration), logger);
                if (dataProtectionProvider is not null)
                {
                    checkUp.Check("Decrypt launcher configuration", () => launcherConfiguration.Decrypt(dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE)), logger);
                }

                var doctorId = Guid.NewGuid();
                var doctorProcessHostMonarch = new DoctorProcessHostMonarch();

                var monarchBuilder = WebApplication.CreateBuilder();
                monarchBuilder.Host
                    .UseSerilog(new LoggerConfiguration().CreateLogger())
                    .ConfigureServices((_, services) =>
                    {
                        checkUp.Check("Setup monarch services", () =>
                        {
                            services.Configure<Microsoft.Extensions.Hosting.HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                            services.AddSingleton(_ => launcherConfiguration);
                            services.AddSingleton(_ => doctorServices.Lifetime);
                            services.AddSingleton(_ => cashboxConfiguration);
                            services.AddSingleton(_ => new Dictionary<Guid, IProcessHostMonarch>() {
                            {
                                doctorId,
                                doctorProcessHostMonarch
                            }
                            });
                            services.AddSingleton(_ => Log.Logger);
                            services.AddSingleton(_ => doctorServices.LauncherExecutablePath);
                        }, logger, throws: true);
                    });

                checkUp.Check("Setup monarch ProcessHostService", () =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        monarchBuilder.WebHost.UseKestrel(serverOptions =>
                        {
                            serverOptions.ListenNamedPipe(commonProperties.LauncherConfiguration.LauncherServiceUri!, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                        });
                    }
                    else
                    {
                        monarchBuilder.WebHost.UseKestrel(serverOptions =>
                        {
                            serverOptions.ListenUnixSocket(commonProperties.LauncherConfiguration.LauncherServiceUri!, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http2;
                            });
                        });
                    }

                    monarchBuilder.Services.AddCodeFirstGrpc();
                }, logger, throws: true);

                var monarchApp = checkUp.Check("Build monarch WebApplication", monarchBuilder.Build, logger, throws: true)!;

                monarchApp.UseRouting();
#pragma warning disable ASP0014
                monarchApp.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());
#pragma warning restore ASP0014

                await checkUp.CheckAwait("Start monarch WebApplication", async () => await WithTimeout(async () => await monarchApp.StartAsync(doctorServices.Lifetime.ApplicationLifetime.ApplicationStopping), TimeSpan.FromSeconds(5)), logger, throws: true);

                var plebeianConfiguration = new PlebeianConfiguration
                {
                    PackageId = doctorId,
                    PackageType = Constants.PackageType.Queue
                };

                var packageConfiguration = new PackageConfiguration
                {
                    Configuration = new(),
                    Id = plebeianConfiguration.PackageId,
                    Package = "none",
                    Url = Array.Empty<string>(),
                    Version = "1.0.0"
                };

                IProcessHostService? processHostService = checkUp.Check("Start plebeian processhostservice client", () =>
                {
                    var handler = new SocketsHttpHandler { ConnectCallback = new IpcConnectionFactory(launcherConfiguration).ConnectAsync };
                    return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = handler }).CreateGrpcService<IProcessHostService>();
                }, logger);

                var plebeianBuilder = Host.CreateDefaultBuilder()
                    .UseSerilog(new LoggerConfiguration().CreateLogger())
                    .ConfigureServices(services =>
                    {
                        checkUp.Check("Setup plebeian services", () =>
                        {
                            services.Configure<Microsoft.Extensions.Hosting.HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
                            services.AddSingleton(_ => launcherConfiguration);
                            services.AddSingleton(_ => packageConfiguration);
                            services.AddSingleton(_ => plebeianConfiguration);

                            var pluginLoader = new PluginLoader();
                            services.AddSingleton(_ => pluginLoader);

                            if (processHostService is not null)
                            {
                                services.AddSingleton(_ => processHostService);
                            }

                            services.AddSingleton<HostingService>();
                            services.AddHostedService<ProcessHostPlebeian>();

                            services.AddSingleton<IClientFactory<IDESSCD>, DESSCDClientFactory>();
                            services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();

                            var bootstrapper = new DoctorMiddlewareBootstrapper
                            (
                                packageConfiguration.Id,
                                packageConfiguration.Configuration.ToDictionary(c => c.Key, c => (object?)c.Value.ToString())!
                            );

                            bootstrapper.ConfigureServices(services);

                            services.AddSingleton(_ => bootstrapper);
                        }, logger, throws: true);
                    });

                var plebeianApp = checkUp.Check("Build plebeian Host", plebeianBuilder.Build, logger, throws: true)!;

                await checkUp.CheckAwait("Start plebeian Host", async () => await WithTimeout(async () => await plebeianApp.StartAsync(doctorServices.Lifetime.ApplicationLifetime.ApplicationStopping), TimeSpan.FromSeconds(5)), logger);

                await doctorProcessHostMonarch.IsStarted.Task;

                await checkUp.CheckAwait("Shutdown launcher gracefully", async () => await WithTimeout(async () =>
                    {
                        await doctorServices.Lifetime.StopAsync(new CancellationToken());

                        await monarchApp.StopAsync();
                        await monarchApp.WaitForShutdownAsync();

                        await plebeianApp.StopAsync();
                        await plebeianApp.WaitForShutdownAsync();
                    }, TimeSpan.FromSeconds(5)), logger
                );
            }
            catch (Exception e)
            {
                checkUp.Failed = true;
                var logger = LoggerExtensions.CreateFromSerilog();
                logger.DoctorFoundErrors(e);
            }

            if (checkUp.Failed)
            {
                return 1;
            }
            else
            {
                var logger = LoggerExtensions.CreateFromSerilog();
                logger.DoctorFoundNoIssues();
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


        public class CheckUp
        {
            public bool Failed { get; set; }

            public async Task<T?> CheckAwait<T>(string operation, Func<Task<T>> action, ILogger logger, bool critical = true, bool throws = false)
            {
                try
                {
                    T result = await action();
                    logger.DoctorCheckSuccess(operation);
                    return result;
                }
                catch (Exception e)
                {
                    logger.DoctorCheckError(e, operation);
                    if (critical)
                    {
                        Failed = true;
                    }
                    if (throws) { throw; }
                    return default;
                }
            }

            public async Task CheckAwait(string operation, Func<Task> action, ILogger logger, bool critical = true, bool throws = false)
            {
                try
                {
                    await action();
                    logger.DoctorCheckSuccess(operation);
                }
                catch (Exception e)
                {
                    logger.DoctorCheckError(e, operation);
                    if (critical)
                    {
                        Failed = true;
                    }
                    if (throws) { throw; }
                }
            }

            public T? Check<T>(string operation, Func<T> action, ILogger logger, bool critical = true, bool throws = false)
            {
                try
                {
                    T result = action();
                    logger.DoctorCheckSuccess(operation);
                    return result;
                }
                catch (Exception e)
                {
                    logger.DoctorCheckError(e, operation);
                    if (critical)
                    {
                        Failed = true;
                    }
                    if (throws) { throw; }
                    return default;
                }
            }

            public bool Check(string operation, Action action, ILogger logger, bool critical = true, bool throws = false)
            {
                try
                {
                    action();
                    logger.DoctorCheckSuccess(operation);
                    return true;
                }
                catch (Exception e)
                {
                    if (critical)
                    {
                        logger.DoctorCheckError(e, operation);
                        Failed = true;
                    }
                    else
                    {
                        logger.DoctorCheckWarning(e, operation);
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

            public void SetPlebeanStarted()
            {
                IsStarted.SetResult();
            }

            public Task GetStopped()
            {
                return Task.CompletedTask;
            }

            public void SetStartupCompleted() { }
        }

        public class DoctorMiddlewareBootstrapper : IMiddlewareBootstrapper
        {
            public Guid Id { get; set; }
            public Dictionary<string, object> Configuration { get; set; }

            public DoctorMiddlewareBootstrapper(Guid id, Dictionary<string, object> configuration)
            {
                Id = id;
                Configuration = configuration;
            }

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
}
