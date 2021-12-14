using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using fiskaltrust.Launcher.AssemblyLoading;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.Clients;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.Logging;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using fiskaltrust.Launcher.PackageDownload;

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--package-config"));
            AddOption(new Option<string>("--plebian-config"));
            AddOption(new Option<string>("--launcher-config"));
            AddOption(new Option<bool>("--no-process-host-service", getDefaultValue: () => false));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public string PackageConfig { get; set; } = null!;
        public string LauncherConfig { get; set; } = null!;
        public string PlebianConfig { get; set; } = null!;
        public bool NoProcessHostService { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfig))) ?? throw new Exception($"Could not deserialize {nameof(LauncherConfig)}");
            var packageConfiguration = JsonSerializer.Deserialize<PackageConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PackageConfig))) ?? throw new Exception($"Could not deserialize {nameof(PackageConfig)}");
            var plebianConfiguration = JsonSerializer.Deserialize<PlebianConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PlebianConfig))) ?? throw new Exception($"Could not deserialize {nameof(PlebianConfig)}");

            IProcessHostService? processHostService = null;
            if (!NoProcessHostService)
            {
                processHostService = GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort!}").CreateGrpcService<IProcessHostService>();
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration, packageConfiguration.Id.ToString())
                .WriteTo.GrpcSink(packageConfiguration, processHostService)
                .CreateLogger();

            var builder = Host.CreateDefaultBuilder()
                .UseSerilog()
                .UseConsoleLifetime()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => launcherConfiguration);
                    services.AddSingleton(_ => packageConfiguration);
                    services.AddSingleton(_ => plebianConfiguration);

                    var pluginLoader = new PluginLoader();
                    services.AddSingleton(_ => pluginLoader);

                    if (processHostService != null)
                    {
                        services.AddSingleton(_ => processHostService);
                    }

                    services.AddSingleton<HostingService>();
                    services.AddHostedService<ProcessHostPlebian>();

                    services.AddSingleton<IClientFactory<IDESSCD>, DESSCDClientFactory>();
                    services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();
                    var downloader = new PackageDownloader(services.BuildServiceProvider().GetRequiredService<ILogger<PackageDownloader>>(), launcherConfiguration);

                    try
                    {
                        var bootstrapper = pluginLoader
                            .LoadComponent<IMiddlewareBootstrapper>(
                                downloader.GetPackagePath(packageConfiguration),
                                new[] {
                                    typeof(IMiddlewareBootstrapper),
                                    typeof(IPOS),
                                    typeof(IDESSCD),
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
                await app.RunAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "An unhandled exception occured");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }
    }
}

