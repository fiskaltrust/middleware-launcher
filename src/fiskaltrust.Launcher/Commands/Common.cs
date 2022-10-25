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

        public LauncherConfiguration LauncherConfiguration = null!;
        protected ftCashBoxConfiguration _cashboxConfiguration = null!;
        protected ECDiffieHellman _clientEcdh = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            _clientEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            LauncherConfiguration = new LauncherConfiguration();

            try
            {
                LauncherConfiguration = Serializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile), SerializerContext.Default);
            }
            catch (Exception e)
            {
                if (!(MergeLegacyConfigIfExists && File.Exists(LegacyConfigFile)))
                {
                    if (File.Exists(LegacyConfigFile))
                    {
                        Log.Warning(e, "Could not parse launcher configuration file \"{LauncherConfigurationFile}\".", LauncherConfigurationFile);
                    }
                    else
                    {
                        Log.Warning("Launcher configuration file \"{LauncherConfigurationFile}\" does not exist.", LauncherConfigurationFile);
                    }
                    Log.Warning("Using command line parameters only.", LauncherConfigurationFile);
                }
            }

            if (MergeLegacyConfigIfExists && File.Exists(LegacyConfigFile))
            {
                LauncherConfiguration.OverwriteWith(await LegacyConfigFileReader.ReadLegacyConfigFile(LegacyConfigFile));

                var configFileDirectory = Path.GetDirectoryName(LauncherConfigurationFile);
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }

                await File.WriteAllTextAsync(LauncherConfigurationFile, JsonSerializer.Serialize(LauncherConfiguration));

                var fi = new FileInfo(LegacyConfigFile);
                fi.CopyTo(LegacyConfigFile + ".legacy");
                fi.Delete();
            }

            LauncherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            LauncherConfiguration.EnableDefaults();

            if (!LauncherConfiguration.UseOffline!.Value && (LauncherConfiguration.CashboxId is null || LauncherConfiguration.AccessToken is null))
            {
                Log.Error("CashBoxId and AccessToken are not provided.");
            }

            try
            {
                var configFileDirectory = Path.GetDirectoryName(LauncherConfiguration.CashboxConfigurationFile);
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create cashbox-configuration-file folder.");
            }

            try
            {
                using var downloader = new ConfigurationDownloader(LauncherConfiguration);
                var exists = await downloader.DownloadConfigurationAsync(_clientEcdh);
                if (LauncherConfiguration.UseOffline!.Value && !exists)
                {
                    Log.Warning("Cashbox configuration was not downloaded because UseOffline is set.");
                }
            }
            catch (Exception e)
            {
                var message = "Could not download Cashbox configuration. ";
                message += $"(Launcher is running in {(LauncherConfiguration.Sandbox!.Value ? "sandbox" : "production")} mode.";
                if (!LauncherConfiguration.Sandbox!.Value)
                {
                    message += " Did you forget the --sandbox flag?";
                }
                message += ")";
                Log.Error(e, message);
            }

            try
            {
                LauncherConfiguration.OverwriteWith(Serializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(LauncherConfiguration.CashboxConfigurationFile!), SerializerContext.Default)?.LauncherConfiguration);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not read Cashbox configuration file.");
            }

            try
            {
                _cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(LauncherConfiguration.CashboxConfigurationFile!)) ?? throw new Exception("Invalid Configuration File");
                _cashboxConfiguration.Decrypt(_clientEcdh, LauncherConfiguration);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not parse Cashbox configuration.");
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(LauncherConfiguration, LauncherConfiguration.CashboxId.HasValue ? new[] { "fiskaltrust.Launcher", LauncherConfiguration.CashboxId.Value.ToString() } : null)
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
            Log.Debug("Cashbox Configuration File: {CashboxConfigurationFile}", LauncherConfiguration.CashboxConfigurationFile);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", LauncherConfiguration.Redacted());

            return 0;
        }
    }
}