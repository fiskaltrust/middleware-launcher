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

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--package-config"));
            AddOption(new Option<string>("--plebian-config"));
            AddOption(new Option<string>("--launcher-config"));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public string PackageConfig { get; set; } = null!;
        public string LauncherConfig { get; set; } = null!;
        public string PlebianConfig { get; set; } = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfig))) ?? throw new Exception($"Could not deserialize {nameof(LauncherConfig)}");
            var packageConfiguration = JsonSerializer.Deserialize<PackageConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PackageConfig))) ?? throw new Exception($"Could not deserialize {nameof(PackageConfig)}");
            var plebianConfiguration = JsonSerializer.Deserialize<PlebianConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PlebianConfig))) ?? throw new Exception($"Could not deserialize {nameof(PlebianConfig)}");

            var builder = Host.CreateDefaultBuilder()
                .UseSerilog((hostingContext, services, loggerConfiguration) =>
                     loggerConfiguration
                        .AddLoggingConfiguration(services, packageConfiguration.Id.ToString())
                        .WriteTo.GrpcSink(services.GetService<IProcessHostService>(), packageConfiguration))
                .UseConsoleLifetime()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => launcherConfiguration);
                    services.AddSingleton(_ => packageConfiguration);
                    services.AddSingleton(_ => plebianConfiguration);

                    services.AddSingleton<PluginLoader>();

                    if (launcherConfiguration.LauncherPort != null)
                    {
                        services.AddSingleton(_ => GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort!}").CreateGrpcService<IProcessHostService>());
                    }

                    var bootstrapper = services
                        .BuildServiceProvider()
                        .GetRequiredService<PluginLoader>()
                        .LoadComponent<IMiddlewareBootstrapper>(
                            Path.Join(launcherConfiguration.ServiceFolder, packageConfiguration.Package, $"{packageConfiguration.Package}.dll"),
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

                    services.AddSingleton<HostingService>();
                    services.AddHostedService<ProcessHostPlebian>();

                    services.AddSingleton<IClientFactory<IDESSCD>, DESSCDClientFactory>();
                    services.AddSingleton<IClientFactory<IPOS>, POSClientFactory>();
                });

            var app = builder.Build();
            await app.RunAsync();

            return 0;
        }
    }
}

