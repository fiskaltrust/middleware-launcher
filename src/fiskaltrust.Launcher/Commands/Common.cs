using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Serilog.Events;

namespace fiskaltrust.Launcher.Commands
{
    public record SubArguments(IEnumerable<string> Args);

    public class CommonCommand : Command
    {
        public CommonCommand(string name, bool addCliOnlyParameters = true) : base(name)
        {
            AddOption(new Option<Guid?>("--cashbox-id"));
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<bool>("--sandbox"));
            AddOption(new Option<string?>("--log-folder"));
            AddOption(new Option<LogLevel?>("--log-level"));

            if (addCliOnlyParameters)
            {
                AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
                AddOption(new Option<string>("--legacy-configuration-file", getDefaultValue: () => "fiskaltrust.exe.config"));
                AddOption(new Option<bool>("--merge-legacy-config-if-exists", getDefaultValue: () => true));
            }
        }
    }

    public class CommonCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;
        public string LegacyConfigurationFile { get; set; } = null!;
        public bool MergeLegacyConfigIfExists { get; set; }

        protected LauncherConfiguration _launcherConfiguration = null!;
        protected ftCashBoxConfiguration _cashboxConfiguration = null!;
        protected ECDiffieHellman _clientEcdh = null!;

        protected IDataProtectionProvider _dataProtectionProvider = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            _launcherConfiguration = new LauncherConfiguration();

            try
            {
                _launcherConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(LauncherConfigurationFile));
            }
            catch (Exception e)
            {
                if (!(MergeLegacyConfigIfExists && File.Exists(LegacyConfigurationFile)))
                {
                    if (File.Exists(LauncherConfigurationFile))
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

            if (MergeLegacyConfigIfExists && File.Exists(LegacyConfigurationFile))
            {
                var legacyConfig = await LegacyConfigFileReader.ReadLegacyConfigFile(LegacyConfigurationFile);
                _launcherConfiguration.OverwriteWith(legacyConfig);

                var configFileDirectory = Path.GetDirectoryName(Path.GetFullPath(LauncherConfigurationFile));
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }

                await File.WriteAllTextAsync(LauncherConfigurationFile, legacyConfig.Serialize(ignoreNullValues: true));

                var fi = new FileInfo(LegacyConfigurationFile);
                fi.CopyTo(LegacyConfigurationFile + ".legacy");
                fi.Delete();
            }

            _launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);

            _launcherConfiguration.EnableDefaults();

            if (!_launcherConfiguration.UseOffline!.Value && (_launcherConfiguration.CashboxId is null || _launcherConfiguration.AccessToken is null))
            {
                Log.Error("CashBoxId and AccessToken are not provided.");
            }

            try
            {
                var configFileDirectory = Path.GetDirectoryName(_launcherConfiguration.CashboxConfigurationFile);
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create cashbox-configuration-file folder.");
            }

            _clientEcdh = await LoadCurve(_launcherConfiguration.AccessToken!, _launcherConfiguration.UseOffline!.Value);

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
                var cashboxConfigurationFile = _launcherConfiguration.CashboxConfigurationFile!;
                _launcherConfiguration.OverwriteWith(LauncherConfigurationInCashBoxConfiguration.Deserialize(await File.ReadAllTextAsync(cashboxConfigurationFile)));
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not read Cashbox configuration file.");
            }

            try
            {
                _cashboxConfiguration = CashBoxConfigurationExt.Deserialize(await File.ReadAllTextAsync(_launcherConfiguration.CashboxConfigurationFile!));
                _cashboxConfiguration.Decrypt(_launcherConfiguration, _clientEcdh);
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


            _dataProtectionProvider = DataProtectionExtensions.Create(_launcherConfiguration.AccessToken);

            try
            {
                _launcherConfiguration.Decrypt(_dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE));
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error decrypring launcher configuration file.");
            }
            return 0;
        }

        public static async Task<ECDiffieHellman> LoadCurve(string accessToken, bool useOffline = false)
        {
            var dataProtector = DataProtectionExtensions.Create(accessToken).CreateProtector(CashBoxConfigurationExt.DATA_PROTECTION_DATA_PURPOSE);
            var clientEcdhPath = Path.Combine(Common.Constants.Paths.CommonFolder, "fiskaltrust.Launcher", "client.ecdh");
            if (File.Exists(clientEcdhPath))
            {
                return ECDiffieHellmanExt.Deserialize(dataProtector.Unprotect(await File.ReadAllTextAsync(clientEcdhPath)));
            }
            else
            {
                const string offlineClientEcdhPath = "/client.ecdh";
                ECDiffieHellman clientEcdh;

                if (useOffline && File.Exists(offlineClientEcdhPath))
                {
                    clientEcdh = ECDiffieHellmanExt.Deserialize(await File.ReadAllTextAsync(offlineClientEcdhPath));
                    try
                    {
                        File.Delete(offlineClientEcdhPath);
                    }
                    catch { }
                }
                else
                {
                    clientEcdh = CashboxConfigEncryption.CreateCurve();
                }

                await File.WriteAllTextAsync(clientEcdhPath, dataProtector.Protect(clientEcdh.Serialize()));

                return clientEcdh;
            }
        }
    }
}