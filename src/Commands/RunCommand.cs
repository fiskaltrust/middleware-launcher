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
            // nullable items are part of the `LauncherConfiguration`
            AddOption(new Option<string?>("--cashbox-id"));
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<int?>("--launcher-port"));
            AddOption(new Option<bool?>("--sandbox"));
            AddOption(new Option<bool?>("--use-offline"));
            AddOption(new Option<string?>("--log-folder"));
            AddOption(new Option<LogLevel?>("--log-level"));
            AddOption(new Option<string?>("--service-folder"));
            AddOption(new Option<Uri?>("--packages-url"));

            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
            AddOption(new Option<string>("--cashbox-configuration-file", getDefaultValue: () => "cashbox.configuration.json"));
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
            var launcherConfiguration = GetDefaultLauncherConfiguration();

            MergeLauncherConfiguration(cashboxLauncherConfiguration, launcherConfiguration);
            MergeLauncherConfiguration(ArgsLauncherConfiguration, launcherConfiguration);
            MergeLauncherConfiguration(JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile)) ?? new LauncherConfiguration(), launcherConfiguration);
            
            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(CashboxConfigurationFile)) ?? throw new Exception("Empty Configuration File");

            var builder = WebApplication.CreateBuilder();
            builder.Host
                .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration.AddLoggingConfiguration(services, launcherConfiguration.CashboxId.ToString()))
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

        private static LauncherConfiguration GetDefaultLauncherConfiguration()
        {
            return new LauncherConfiguration {
                LauncherPort = 3000,
                Sandbox = false,
                UseOffline = false,
                LogFolder = Path.Join(Paths.ServiceFolder, "logs"),
                LogLevel =  LogLevel.Information,
                ServiceFolder = Paths.ServiceFolder,
                PackagesUrl = new Uri("https://packages.fiskaltrust.cloud")
            };
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
