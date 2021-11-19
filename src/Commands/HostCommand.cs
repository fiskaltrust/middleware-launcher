using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using fiskaltrust.Launcher.AssemblyLoading;
using fiskaltrust.Middleware.Abstractions;

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<string>("--package-config"));
            AddOption(new Option<PackageType>("--package-type"));
            AddOption(new Option<string>("--launcher-config"));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public string PackageConfig { get; set; } = null!;
        public string LauncherConfig { get; set; } = null!;
        public PackageType PackageType { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfig))) ?? throw new Exception($"Could not deserialize {nameof(LauncherConfig)}");
            var packageConfiguration = JsonSerializer.Deserialize<PackageConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PackageConfig))) ?? throw new Exception($"Could not deserialize {nameof(PackageConfig)}");
            var bootstrapper = PluginLoader.LoadPlugin<IMiddlewareBootstrapper>(launcherConfiguration.ServiceFolder!, packageConfiguration.Package);
            bootstrapper.Id = packageConfiguration.Id;
            bootstrapper.Configuration = packageConfiguration.Configuration.ToDictionary(x => x.Key, x => (object?)x.Value.ToString());

            var builder = Host.CreateDefaultBuilder()
                .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration.AddLoggingConfiguration())
                .UseConsoleLifetime()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => launcherConfiguration);
                    services.AddSingleton(_ => packageConfiguration);
                    services.AddSingleton(_ => new PlebianConfiguration { PackageType = PackageType });

                    services.AddSingleton<HostingService>();
                    services.AddHostedService<ProcessHostPlebian>();

                    bootstrapper.ConfigureServices(services);
                });

            var app = builder.Build();
            await app.RunAsync();

            return 0;
        }
    }
}
