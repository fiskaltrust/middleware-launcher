using System.CommandLine;
using System.CommandLine.Invocation;
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

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--plebeian-configuration"));
            AddOption(new Option<string>("--debugging"));
            AddOption(new Option<string>("--launcher-configuration"));
            AddOption(new Option<bool>("--no-process-host-service", getDefaultValue: () => false));
            AddOption(new Option<bool>("--use-domain-sockets"));
            AddOption(new Option<string>("--domain-socket-path"));
            AddOption(new Option<bool>("--use-named-pipes"));
            AddOption(new Option<string?>("--named-pipe-name"));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public string LauncherConfiguration { get; set; } = null!;
        public string PlebeianConfiguration { get; set; } = null!;
        public bool NoProcessHostService { get; set; }
        public bool Debugging { get; set; }
        
        public bool UseDomainSockets { get; }
        public string? DomainSocketPath { get; }

        private readonly CancellationToken _cancellationToken;
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public HostCommandHandler(IHostApplicationLifetime lifetime, LauncherExecutablePath launcherExecutablePath)
        {
            _cancellationToken = lifetime.ApplicationStopping;
            _launcherExecutablePath = launcherExecutablePath;
            UseDomainSockets = useDomainSockets;
            DomainSocketPath = domainSocketPath;
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

            var launcherConfigurationBase64Decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfiguration));
            var launcherConfiguration = Common.Configuration.LauncherConfiguration.Deserialize(launcherConfigurationBase64Decoded);
    
            launcherConfiguration = launcherConfiguration with 
            {
                UseDomainSockets = UseDomainSockets,
                DomainSocketPath = UseDomainSockets ? DomainSocketPath ?? throw new InvalidOperationException("Domain socket path must be provided when using domain sockets.") : null
            };
    
            var plebeianConfigurationBase64Decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PlebeianConfiguration));
            var plebeianConfiguration = Configuration.PlebeianConfiguration.Deserialize(plebeianConfigurationBase64Decoded);

            var cashboxConfiguration = CashBoxConfigurationExt.Deserialize(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));

            cashboxConfiguration.Decrypt(launcherConfiguration, await CommonCommandHandler.LoadCurve(launcherConfiguration.AccessToken!, launcherConfiguration.UseLegacyDataProtection!.Value));

            var packageConfiguration = (plebeianConfiguration.PackageType switch
            {
                PackageType.Queue => cashboxConfiguration.ftQueues,
                PackageType.SCU => cashboxConfiguration.ftSignaturCreationDevices,
                PackageType.Helper => cashboxConfiguration.helpers,
                var unknown => throw new Exception($"Unknown PackageType {unknown}")
            }).First(p => p.Id == plebeianConfiguration.PackageId);

            packageConfiguration.Configuration = ProcessPackageConfiguration(packageConfiguration.Configuration, launcherConfiguration, cashboxConfiguration);

            IProcessHostService? processHostService = null;
            if (!NoProcessHostService)
            {
                processHostService = GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort}").CreateGrpcService<IProcessHostService>();
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
                    services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
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
                    services.AddSingleton<IClientFactory<IITSSCD>, ITSSCDClientFactory>();
                    services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();

                    using var downloader = new PackageDownloader(services.BuildServiceProvider().GetRequiredService<ILogger<PackageDownloader>>(), launcherConfiguration, _launcherExecutablePath);

                    try
                    {
                        var bootstrapper = pluginLoader
                            .LoadComponent<IMiddlewareBootstrapper>(
                                downloader.GetPackagePath(packageConfiguration),
                                new[] {
                                    typeof(IMiddlewareBootstrapper),
                                    typeof(IPOS),
                                    typeof(IDESSCD),
                                    typeof(IITSSCD),
                                    typeof(IClientFactory<IPOS>),
                                    typeof(IClientFactory<IDESSCD>),
                                    typeof(IClientFactory<IITSSCD>),
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
                    } 
                });

            try
            {
                var app = builder.Build();
                await app.RunAsync(_cancellationToken);
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occurred.");
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

