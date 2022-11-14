using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using fiskaltrust.Launcher.AssemblyLoading;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.Clients;
using fiskaltrust.Launcher.Logging;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Constants;
using System.Diagnostics;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Services.Interfaces;

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--plebian-configuration"));
            AddOption(new Option<string>("--debugging"));
            AddOption(new Option<string>("--launcher-configuration"));
            AddOption(new Option<bool>("--no-process-host-service", getDefaultValue: () => false));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public string LauncherConfiguration { get; set; } = null!;
        public string PlebianConfiguration { get; set; } = null!;
        public bool NoProcessHostService { get; set; }
        public bool Debugging { get; set; }

        private readonly CancellationToken _cancellationToken;

        public HostCommandHandler(IHostApplicationLifetime lifetime)
        {
            _cancellationToken = lifetime.ApplicationStopping;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            if (Debugging)
            {
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(100);
                }
            }

            var launcherConfiguration = Common.Configuration.LauncherConfiguration.Deserialize(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfiguration)));
            launcherConfiguration.EnableDefaults();

            var plebianConfiguration = Configuration.PlebianConfiguration.Deserialize(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PlebianConfiguration)));

            var cashboxConfiguration = CashBoxConfigurationExt.Deserialize(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));

            var packageConfiguration = (plebianConfiguration.PackageType switch
            {
                PackageType.Queue => cashboxConfiguration.ftQueues,
                PackageType.SCU => cashboxConfiguration.ftSignaturCreationDevices,
                PackageType.Helper => cashboxConfiguration.helpers,
                var unknown => throw new Exception($"Unknown PackageType {unknown}")
            }).First(p => p.Id == plebianConfiguration.PackageId);

            packageConfiguration.Configuration = packageConfiguration.Configuration.Union(DefaultPackageConfig(launcherConfiguration, cashboxConfiguration)).ToDictionary(k => k.Key, v => v.Value);


            IProcessHostService? processHostService = null;
            if (!NoProcessHostService)
            {
                processHostService = GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort}").CreateGrpcService<IProcessHostService>();
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration, new[] { packageConfiguration.Package, packageConfiguration.Id.ToString() })
                .WriteTo.GrpcSink(packageConfiguration, processHostService)
                .CreateLogger();

            var builder = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
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

                    using var downloader = new PackageDownloader(services.BuildServiceProvider().GetRequiredService<ILogger<PackageDownloader>>(), launcherConfiguration);

                    try
                    {
                        var bootstrapper = pluginLoader
                            .LoadComponent<IMiddlewareBootstrapper>(
                                downloader.GetPackagePath(packageConfiguration),
                                new[] {
                                    typeof(IMiddlewareBootstrapper),
                                    typeof(IPOS),
                                    typeof(IDESSCD),
                                    typeof(IClientFactory<IPOS>),
                                    typeof(IClientFactory<IDESSCD>),
                                    typeof(JournalRequest),
                                    typeof(JournalResponse),
                                    typeof(IHelper),
                                    typeof(IServiceCollection),
                                    typeof(Microsoft.Extensions.Logging.ILogger),
                                    typeof(ILoggerFactory),
                                    typeof(ILogger<>)
                            });

                        bootstrapper.Id = packageConfiguration.Id;
                        bootstrapper.Configuration = packageConfiguration.Configuration.ToDictionary(c => c.Key, c => (object?)c.Value.ToString());

                        bootstrapper.ConfigureServices(services);

                        services.AddSingleton(_ => bootstrapper);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Could not load {Type}.", nameof(IMiddlewareBootstrapper));
                        throw;
                    } // Will also be detected and logged propperly later
                });

            try
            {
                var app = builder.Build();
                var messageBusService = new MessageBusService(app.Services.GetService<ILogger<MessageBusService>>(), launcherConfiguration);
                await messageBusService.StartMQQTServer();

                await app.RunAsync(_cancellationToken);
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured.");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }


        private static Dictionary<string, object> DefaultPackageConfig(LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashboxConfiguration)
        {
            var config = new Dictionary<string, object>
            {
                { "cashboxid", launcherConfiguration.CashboxId! },
                { "accesstoken", launcherConfiguration.AccessToken! },
                { "useoffline", launcherConfiguration.UseOffline!.Value },
                { "sandbox", launcherConfiguration.Sandbox! },
                { "configuration", cashboxConfiguration.Serialize() },
                { "servicefolder", Path.Combine(launcherConfiguration.ServiceFolder!, "service") },
            };

            if (launcherConfiguration.Proxy is not null)
            {
                config.Add("proxy", launcherConfiguration.Proxy!);
            }

            return config;
        }
    }
}

