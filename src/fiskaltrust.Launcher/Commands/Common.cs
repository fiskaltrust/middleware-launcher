using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.storage.serialization.V0;
using Serilog;

namespace fiskaltrust.Launcher.Commands
{
    public record SubArguments(IEnumerable<string> Args);

    public class CommonCommand : Command
    {
        public CommonCommand(string name) : base(name)
        {
            AddOption(new Option<Guid?>("--cashbox-id"));
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<bool>("--sandbox"));
            AddOption(new Option<string?>("--log-folder"));
            AddOption(new Option<LogLevel?>("--log-level"));
            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
        }
    }

    public class CommonCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;

        protected LauncherConfiguration _launcherConfiguration = null!;
        protected ftCashBoxConfiguration _cashboxConfiguration = null!;
        protected ECDiffieHellman _clientEcdh = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            _clientEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

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
                var cashboxDirectory = Path.GetDirectoryName(_launcherConfiguration.CashboxConfigurationFile);
                Directory.CreateDirectory(cashboxDirectory!);
            }
            catch (Exception e)
            {
                errors.Add(("Could not create Cashbox directory", e));
            }

            try
            {
                using var downloader = new ConfigurationDownloader(_launcherConfiguration);
                await downloader.DownloadConfigurationAsync(_clientEcdh);
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
                _cashboxConfiguration.Decrypt(_clientEcdh, _launcherConfiguration);
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
}