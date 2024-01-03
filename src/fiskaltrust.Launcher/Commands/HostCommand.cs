using System.CommandLine;
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
using fiskaltrust.ifPOS.v1.it;
using fiskaltrust.Launcher.Helpers;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--plebeian-configuration"));
            AddOption(new Option<bool>("--debugging"));
            AddOption(new Option<string>("--launcher-configuration"));
            AddOption(new Option<bool>("--no-process-host-service", getDefaultValue: () => false));
        }

        public class HostOptions
        {
            public HostOptions(string launcherConfiguration, string plebeianConfiguration, bool noProcessHostService, bool debugging, string? namedPipeName, bool useNamedPipes, string? domainSocketPath, bool useDomainSockets)
            {
                LauncherConfiguration = launcherConfiguration;
                PlebeianConfiguration = plebeianConfiguration;
                NoProcessHostService = noProcessHostService;
                Debugging = debugging;
                NamedPipeName = namedPipeName;
                UseNamedPipes = useNamedPipes;
                DomainSocketPath = domainSocketPath;
                UseDomainSockets = useDomainSockets;
            }

            public readonly string LauncherConfiguration;
            public readonly string PlebeianConfiguration;
            public readonly bool NoProcessHostService;
            public readonly bool Debugging;
            public readonly bool UseDomainSockets;
            public readonly string? DomainSocketPath;
            public readonly bool UseNamedPipes;
            public readonly string? NamedPipeName;
        }

        public class HostServices
        {
            public HostServices(LauncherExecutablePath launcherExecutablePath, IHostApplicationLifetime lifetime)
            {
                CancellationToken = lifetime.ApplicationStopping;
                LauncherExecutablePath = launcherExecutablePath;
            }

            public CancellationToken CancellationToken { get; set; }
            public LauncherExecutablePath LauncherExecutablePath { get; set; }
        }

        public static class HostHandler
        {
            public static async Task<int> HandleAsync(HostOptions hostOptions, HostServices hostServices)
            {
                if (hostOptions.Debugging)
                {
                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(100);
                    }
                }

                var launcherConfiguration = LauncherConfiguration.Deserialize(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(hostOptions.LauncherConfiguration)));
                var plebeianConfiguration = PlebeianConfiguration.Deserialize(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(hostOptions.PlebeianConfiguration)));
                
                launcherConfiguration = launcherConfiguration with 
                {
                    UseDomainSockets = hostOptions.UseDomainSockets,
                    DomainSocketPath = hostOptions.DomainSocketPath,
                    UseNamedPipes = hostOptions.UseNamedPipes,
                    NamedPipeName = hostOptions.NamedPipeName
                };

                var cashboxConfiguration = CashBoxConfigurationExt.Deserialize(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));
                cashboxConfiguration.Decrypt(launcherConfiguration, await CommonHandler.LoadCurve(launcherConfiguration.CashboxId.Value, launcherConfiguration.AccessToken!, launcherConfiguration.ServiceFolder!));
                
                var packageConfiguration = (plebeianConfiguration.PackageType switch
                {
                    PackageType.Queue => cashboxConfiguration.ftQueues,
                    PackageType.SCU => cashboxConfiguration.ftSignaturCreationDevices,
                    PackageType.Helper => cashboxConfiguration.helpers,
                    _ => throw new Exception($"Unknown PackageType {plebeianConfiguration.PackageType}")
                }).First(p => p.Id == plebeianConfiguration.PackageId);

                packageConfiguration.Configuration = ProcessPackageConfiguration(packageConfiguration.Configuration, launcherConfiguration, cashboxConfiguration);

                IProcessHostService? processHostService = null;
                if (!hostOptions.NoProcessHostService)
                {
                    string grpcAddress = launcherConfiguration.UseDomainSockets ? "http://unix/" : $"http://localhost:{launcherConfiguration.LauncherPort}";
                    processHostService = GrpcChannel.ForAddress(grpcAddress).CreateGrpcService<IProcessHostService>();
                }

                Log.Logger = new LoggerConfiguration()
                    .AddLoggingConfiguration(launcherConfiguration)
                    .WriteTo.GrpcSink(packageConfiguration, processHostService)
                    .CreateLogger();

                System.Text.Encoding.RegisterProvider(new LauncherEncodingProvider());

                var builder = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(_ => launcherConfiguration);
                        services.AddSingleton(_ => packageConfiguration);
                        services.AddSingleton(_ => plebeianConfiguration);
                        
                        services.Configure<Microsoft.Extensions.Hosting.HostOptions>(opts =>
                        {
                            opts.ShutdownTimeout = TimeSpan.FromSeconds(30);
                        });

                        var pluginLoader = new PluginLoader();
                        services.AddSingleton(_ => pluginLoader);

                        if (processHostService is not null)
                        {
                            services.AddSingleton(_ => processHostService);
                        }

                        services.AddSingleton<HostingService>();
                        services.AddHostedService<ProcessHostPlebeian>();

                        services.AddSingleton<IClientFactory<IDESSCD>, DESSCDClientFactory>();
                        services.AddSingleton<IClientFactory<IITSSCD>, ITSSCDClientFactory>();
                        services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();

                        using var downloader = new PackageDownloader(services.BuildServiceProvider().GetRequiredService<ILogger<PackageDownloader>>(), launcherConfiguration, hostServices.LauncherExecutablePath);

                        try
                        {
                            var bootstrapper = pluginLoader.LoadComponent<IMiddlewareBootstrapper>(downloader.GetPackagePath(packageConfiguration), new[] { typeof(IMiddlewareBootstrapper), typeof(IPOS), typeof(IDESSCD), typeof(IITSSCD), typeof(IClientFactory<IPOS>), typeof(IClientFactory<IDESSCD>), typeof(IClientFactory<IITSSCD>), typeof(JournalRequest), typeof(JournalResponse), typeof(IHelper), typeof(IServiceCollection), typeof(ILogger), typeof(ILoggerFactory), typeof(ILogger<>) });
                            bootstrapper.Id = packageConfiguration.Id;
                            bootstrapper.Configuration = packageConfiguration.Configuration.ToDictionary(c => c.Key, c => (object?)c.Value.ToString());
                            bootstrapper.ConfigureServices(services);
                            services.AddSingleton(_ => bootstrapper);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Could not load {Type}.", nameof(IMiddlewareBootstrapper));
                            throw;
                        }
                    });

                try
                {
                    var app = builder.Build();
                    await app.RunAsync(hostServices.CancellationToken);
                }
                catch (Exception e)
                {
                    Log.Error(e, "An unhandled exception occured.");
                    throw;
                }
                finally
                {
                    Log.CloseAndFlush();
                }

                return 0;
            }

            private static Dictionary<string, object> ProcessPackageConfiguration(Dictionary<string, object> configuration, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashboxConfiguration)
            {
                var defaults = new Dictionary<string, object>
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
                    defaults.Add("proxy", launcherConfiguration.Proxy!);
                }

                foreach (var entry in defaults)
                {
                    if (!configuration.ContainsKey(entry.Key))
                    {
                        configuration.Add(entry.Key, entry.Value);
                    }
                }

                foreach (var entry in configuration.Where(entry => entry.Key.ToLower() == "connectionstring"))
                {
                    configuration[entry.Key] = $"raw:{entry.Value}";
                }

                return configuration;
            }
        }
    }
}
