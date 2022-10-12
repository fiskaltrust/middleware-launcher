using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using Serilog.Events;
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
            AddOption(new Option<string>("--legacy-config-file", getDefaultValue: () => "fiskaltrust.exe.config"));
            AddOption(new Option<bool>("--merge-legacy-config-if-exists", getDefaultValue: () => true));
        }
    }

    public class CommonCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;
        public string LegacyConfigFile { get; set; } = null!;
        public bool MergeLegacyConfigIfExists { get; set; }

        protected LauncherConfiguration _launcherConfiguration = null!;
        protected ftCashBoxConfiguration _cashboxConfiguration = null!;
        protected ECDiffieHellman _clientEcdh = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            _clientEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            if (MergeLegacyConfigIfExists && File.Exists(LegacyConfigFile))
            {
                _launcherConfiguration = await LegacyConfigFileReader.ReadLegacyConfigFile(errors, LegacyConfigFile);
                if (_launcherConfiguration != null)
                {
                    await File.WriteAllTextAsync(LauncherConfigurationFile, JsonSerializer.Serialize(_launcherConfiguration));
                    FileInfo fi = new FileInfo(LegacyConfigFile);
                    fi.CopyTo(LegacyConfigFile + ".legacy");
                    fi.Delete();
                }
            }
            else
            {
                _launcherConfiguration = new LauncherConfiguration();
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
            }

            _launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            try
            {
                var cashboxDirectory = Path.GetDirectoryName(_launcherConfiguration.CashboxConfigurationFile);
                Directory.CreateDirectory(cashboxDirectory!);
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create Cashbox directory.");
            }

            try
            {
                using var downloader = new ConfigurationDownloader(_launcherConfiguration);
                var exists = await downloader.DownloadConfigurationAsync(_clientEcdh);
                if (_launcherConfiguration.UseOffline!.Value && !exists)
                {
                    Log.Warning("Cashbox configuration was not downloaded because UseOffline is set.");
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
                Log.Error(e, message);
            }

            try
            {
                _launcherConfiguration.OverwriteWith(Serializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!), SerializerContext.Default)?.LauncherConfiguration);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not read Cashbox configuration file.");
            }

            try
            {
                _cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!)) ?? throw new Exception("Invalid Configuration File");
                _cashboxConfiguration.Decrypt(_clientEcdh, _launcherConfiguration);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not parse Cashbox configuration.");
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(_launcherConfiguration, _launcherConfiguration.CashboxId.HasValue ? new[] { "fiskaltrust.Launcher", _launcherConfiguration.CashboxId.Value.ToString() } : null)
                .Enrich.FromLogContext()
                .CreateLogger();

            foreach (var logEvent in collectionSink.Events)
            {
                Log.Write(logEvent);
            }

            if (collectionSink.Events.Where(e => e.Level == LogEventLevel.Fatal).Any())
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