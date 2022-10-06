using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using Serilog.Extensions.Logging;

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

            List<(LogLevel logLevel, string message, Exception? e)> errors = new();

            try
            {
                _launcherConfiguration.OverwriteWith(Serializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile), SerializerContext.Default) ?? new LauncherConfiguration());
            }
            catch (DirectoryNotFoundException e)
            {
                errors.Add((LogLevel.Warning, $"Launcher configuration file \"{LauncherConfigurationFile}\" does not exist, using command line parameters only.", e));
            }
            catch (Exception e)
            {
                errors.Add((LogLevel.Critical, $"Could not read launcher configuration file \"{LauncherConfigurationFile}\"", e));
            }

            _launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            try
            {
                var cashboxDirectory = Path.GetDirectoryName(_launcherConfiguration.CashboxConfigurationFile);
                Directory.CreateDirectory(cashboxDirectory!);
            }
            catch (Exception e)
            {
                errors.Add((LogLevel.Error, "Could not create Cashbox directory.", e));
            }

            try
            {
                using var downloader = new ConfigurationDownloader(_launcherConfiguration);
                var exists = await downloader.DownloadConfigurationAsync(_clientEcdh);
                if (_launcherConfiguration.UseOffline!.Value && !exists)
                {
                    errors.Add((LogLevel.Warning, "Cashbox configuration was not downloaded because UseOffline is set.", null));
                }
            }
            catch (Exception e)
            {
                var message = "Could not download Cashbox configuration. ";
                message += $"(Launcher is running in {(_launcherConfiguration.Sandbox!.Value ? "sandbox" : "production")} mode.";
                if (!_launcherConfiguration.Sandbox!.Value)
                {
                    message += " Did you forget the --sandbox flag?";
                }
                message += ")";
                errors.Add((LogLevel.Error, message, e));
            }

            try
            {
                _launcherConfiguration.OverwriteWith(Serializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!), SerializerContext.Default)?.LauncherConfiguration);
            }
            catch (Exception e)
            {
                errors.Add((LogLevel.Critical, "Could not read Cashbox configuration file.", e));
            }

            try
            {
                _cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!)) ?? throw new Exception("Invalid Configuration File");
                _cashboxConfiguration.Decrypt(_clientEcdh, _launcherConfiguration);
            }
            catch (Exception e)
            {
                errors.Add((LogLevel.Critical, "Could not parse Cashbox configuration.", e));
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(_launcherConfiguration, _launcherConfiguration.CashboxId.HasValue ? new[] { "fiskaltrust.Launcher", _launcherConfiguration.CashboxId.Value.ToString() } : null)
                .Enrich.FromLogContext()
                .CreateLogger();

            foreach (var (logLevel, message, e) in errors.AsEnumerable())
            {
                Log.Write(LevelConvert.ToSerilogLevel(logLevel), e, message);
            }

            if (errors.Where(e => e.logLevel == LogLevel.Critical).Any())
            {
                return 1;
            }

            Log.Debug("Launcher Configuration File: {LauncherConfigurationFile}", LauncherConfigurationFile);
            Log.Debug("Cashbox Configuration File: {CashboxConfigurationFile}", _launcherConfiguration.CashboxConfigurationFile);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", _launcherConfiguration.Redacted());

            return 0;
        }
    }
}