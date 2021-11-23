using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore;
using Serilog;
using ProtoBuf.Grpc.Server;
using System.CommandLine.Hosting;

namespace fiskaltrust.Launcher.Commands
{
    public class RunCommand : Command
    {
        public RunCommand() : base("run")
        {
            AddOption(new Option<string?>("--cashbox-id", getDefaultValue: () => null));
            AddOption(new Option<string?>("--access-token", getDefaultValue: () => null));
            AddOption(new Option<int?>("--launcher-port", getDefaultValue: () => null));
            AddOption(new Option<string?>("--service-folder", getDefaultValue: () => Paths.ServiceFolder));
            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "configuration.json"));
            AddOption(new Option<string>("--cashbox-configuration-file", getDefaultValue: () => "configuration.json"));
        }
    }

    public class RunCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;
        public string CashboxConfigurationFile { get; set; } = null!;


        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var cashboxLauncherConfiguration = JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(CashboxConfigurationFile))?.LauncherConfiguration;
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile)) ?? new LauncherConfiguration();

            MergeLauncherConfiguration(cashboxLauncherConfiguration, launcherConfiguration);
            MergeLauncherConfiguration(ArgsLauncherConfiguration, launcherConfiguration);

            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(CashboxConfigurationFile)) ?? throw new Exception("Empty Configuration File");

            var builder = WebApplication.CreateBuilder();
            builder.Host
                .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration.AddLoggingConfiguration())
                .UseConsoleLifetime()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(_ => launcherConfiguration);
                    services.AddSingleton(_ => cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddHostedService<ProcessHostMonarcStartup>();
                });

            if (launcherConfiguration.LauncherPort == null)
            {
                throw new Exception("Launcher port cannot be null.");
            }
            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{launcherConfiguration.LauncherPort!}")));


            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());

            await app.StartAsync();


            await app.WaitForShutdownAsync();

            return 0;
        }

        private static void MergeLauncherConfiguration(LauncherConfiguration? source, LauncherConfiguration? target)
        {
            if (source != null && target != null)
            {
                foreach (var property in typeof(LauncherConfiguration).GetProperties())
                {
                    var value = property.GetValue(source, null);

                    if (value != null)
                    {
                        property.SetValue(target, value, null);
                    }
                }
            }
        }
    }
}
