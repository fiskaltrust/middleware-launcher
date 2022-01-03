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
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using Microsoft.Extensions.Hosting.WindowsServices;

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
            AddOption(new Option<bool>("--sandbox"));
            AddOption(new Option<bool?>("--use-offline"));
            AddOption(new Option<string?>("--log-folder"));
            AddOption(new Option<LogLevel?>("--log-level"));
            AddOption(new Option<string?>("--service-folder"));
            AddOption(new Option<Uri?>("--packages-url"));
            AddOption(new Option<Uri?>("--helipad-url"));
            AddOption(new Option<int?>("--download-timeout-sec"));
            AddOption(new Option<int?>("--download-retry"));
            AddOption(new Option<bool?>("--ssl-validation"));
            AddOption(new Option<string?>("--proxy"));
            AddOption(new Option<string?>("--processhost-ping-period"));
            AddOption(new Option<string?>("--cashbox-configuration-file"));

            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
        }
    }

    public class RunCommandHandler : ICommandHandler
    {
        public class AlreadyLoggedException : Exception { }

        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;

        private readonly CancellationToken _cancellationToken;

        public RunCommandHandler(IHostApplicationLifetime lifetime)
        {
            _cancellationToken = lifetime.ApplicationStopping;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var launcherConfiguration = new LauncherConfiguration();

            MergeLauncherConfiguration(JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile)) ?? new LauncherConfiguration(), launcherConfiguration);
            MergeLauncherConfiguration(ArgsLauncherConfiguration, launcherConfiguration);

            Exception? configDownloadException = null;
            try
            {
                await new Downloader(null, launcherConfiguration).DownloadConfiguration();
            }
            catch (Exception e)
            {
                configDownloadException = e;
            }

            MergeLauncherConfiguration(JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile))?.LauncherConfiguration, launcherConfiguration);

            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile)) ?? throw new Exception("Invalid Configuration File");

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration, launcherConfiguration.CashboxId.ToString())
                .Enrich.FromLogContext()
                .CreateLogger();

            Log.Debug($"Launcher Configuration: {JsonSerializer.Serialize(launcherConfiguration)}");

            if (configDownloadException != null)
            {
                Log.Error(configDownloadException, "Could not update Cashbox configuration");
            }

            var builder = WebApplication.CreateBuilder();
            builder.Host
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(_ => launcherConfiguration);
                    services.AddSingleton(_ => cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddSingleton<Downloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                });

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{launcherConfiguration.LauncherPort}")));

            builder.Services.AddCodeFirstGrpc();

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<ProcessHostService>());


            try
            {
                await app.RunAsync(_cancellationToken);
            }
            catch (AlreadyLoggedException)
            {
                return 1;
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
