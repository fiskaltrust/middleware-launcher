using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;

namespace fiskaltrust.Launcher.Commands
{
    public class CommonRunCommand : Command
    {
        public CommonRunCommand(string name) : base(name)
        {
            AddOption(new Option<Guid?>("--cashbox-id"));
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<int?>("--launcher-port"));
            AddOption(new Option<bool>("--sandbox"));
            AddOption(new Option<bool>("--use-offline"));
            AddOption(new Option<string?>("--log-folder"));
            AddOption(new Option<LogLevel?>("--log-level"));
            AddOption(new Option<string?>("--service-folder"));
            AddOption(new Option<Uri?>("--packages-url"));
            AddOption(new Option<Uri?>("--helipad-url"));
            AddOption(new Option<int?>("--download-timeout-sec"));
            AddOption(new Option<int?>("--download-retry"));
            AddOption(new Option<bool>("--ssl-validation"));
            AddOption(new Option<string?>("--proxy"));
            AddOption(new Option<string?>("--processhost-ping-period-sec"));
            AddOption(new Option<string?>("--cashbox-configuration-file"));

            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
        }
    }

    public class RunCommand : CommonRunCommand
    {
        public RunCommand() : base("run") { }
    }

    public class CommonRunCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;

        protected LauncherConfiguration _launcherConfiguration = null!;
        protected ftCashBoxConfiguration _cashboxConfiguration = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            _launcherConfiguration = new LauncherConfiguration();

            List<(string message, Exception? e)> fatal = new();
            List<(string message, Exception? e)> errors = new();
            List<(string message, Exception? e)> warnings = new();

            try
            {
                _launcherConfiguration.OverwriteWith(JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile)) ?? new LauncherConfiguration());
            }
            catch (Exception e)
            {
                warnings.Add(("Could not read launcher configuration file", e));
            }

            _launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            try
            {
                await new Downloader(null, _launcherConfiguration).DownloadConfiguration();
            }
            catch (Exception e)
            {
                errors.Add(("Could not update Cashbox configuration", e));
            }

            try
            {
                _launcherConfiguration.OverwriteWith(JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!))?.LauncherConfiguration);
            }
            catch (Exception e)
            {
                fatal.Add(("Could not read cashbox configuration file", e));
            }

            try
            {
                _cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!)) ?? throw new Exception("Invalid Configuration File");
            }
            catch (Exception e)
            {
                fatal.Add(("Could not parse cashbox configuration", e));
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(_launcherConfiguration, _launcherConfiguration.CashboxId.ToString())
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

            Log.Debug("Launcher Configuration File: {LauncherConfigurationFile}", LauncherConfigurationFile);
            Log.Debug("Cashbox Configuration File: {CashboxConfigurationFile}", _launcherConfiguration.CashboxConfigurationFile);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", _launcherConfiguration);

            return 0;
        }
    }

    public class RunCommandHandler : CommonRunCommandHandler
    {
        public class AlreadyLoggedException : Exception { }

        private readonly CancellationToken _cancellationToken;

        public RunCommandHandler(IHostApplicationLifetime lifetime)
        {
            _cancellationToken = lifetime.ApplicationStopping;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if(await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

            var builder = WebApplication.CreateBuilder();
            builder.Host
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(_ => _launcherConfiguration);
                    services.AddSingleton(_ => _cashboxConfiguration);
                    services.AddSingleton(_ => new Dictionary<Guid, ProcessHostMonarch>());
                    services.AddSingleton<Downloader>();
                    services.AddHostedService<ProcessHostMonarcStartup>();
                    services.AddSingleton(_ => Log.Logger);
                });

            builder.WebHost.ConfigureKestrel(options => HostingService.ConfigureKestrel(options, new Uri($"http://[::1]:{_launcherConfiguration.LauncherPort}")));

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
    }
}
