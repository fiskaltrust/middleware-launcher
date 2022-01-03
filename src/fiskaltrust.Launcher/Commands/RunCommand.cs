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

            List<(string message, Exception? e)> fatal = new();
            List<(string message, Exception? e)> errors = new();
            List<(string message, Exception? e)> warnings = new();

            try
            {
                MergeLauncherConfiguration(JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile)) ?? new LauncherConfiguration(), launcherConfiguration);
            }
            catch (Exception e)
            {
                warnings.Add(("Could not read launcher configuration file", e));
            }

            MergeLauncherConfiguration(ArgsLauncherConfiguration, launcherConfiguration);

            try
            {
                await new Downloader(null, launcherConfiguration).DownloadConfiguration();
            }
            catch (Exception e)
            {
                errors.Add(("Could not update Cashbox configuration", e));
            }

            try
            {
                MergeLauncherConfiguration(JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile))?.LauncherConfiguration, launcherConfiguration);
            }
            catch (Exception e)
            {
                fatal.Add(("Could not read cashbox configuration file", e));
            }

            ftCashBoxConfiguration cashboxConfiguration = null!;
            try
            {
                cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile)) ?? throw new Exception("Invalid Configuration File");
            }
            catch (Exception e)
            {
                fatal.Add(("Could not parse cashbox configuration", e));
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration, launcherConfiguration.CashboxId.ToString())
                .Enrich.FromLogContext()
                .CreateLogger();

            foreach (var (message, e) in fatal.Concat(errors))
            {
                Log.Error(e, message);
            }

            foreach (var (message, e) in warnings)
            {
                Log.Warning(e, message);
            }

            if (fatal.Count > 0)
            {
                return 1;
            }

            Log.Debug($"Launcher Configuration File: {LauncherConfigurationFile}");
            Log.Debug($"Cashbox Configuration File: {launcherConfiguration.CashboxConfigurationFile}");
            Log.Debug($"Launcher Configuration: {JsonSerializer.Serialize(launcherConfiguration)}");


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
