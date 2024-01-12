using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Constants;
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

            var logLevelOption = new Option<LogLevel?>("--log-level", "Set the log level of the application.");
            logLevelOption.AddAlias("-v");
            logLevelOption.AddAlias("--verbosity");
            AddOption(logLevelOption);

            if (addCliOnlyParameters)
            {
                AddOption(new Option<string>("--launcher-configuration-file",
                    getDefaultValue: () => Paths.LauncherConfigurationFileName));
                AddOption(new Option<string>("--legacy-configuration-file",
                    getDefaultValue: () => Paths.LegacyConfigurationFileName));
                AddOption(new Option<bool>("--merge-legacy-config-if-exists", getDefaultValue: () => true));
            }
        }
    }

    public class CommonOptions
    {
        public CommonOptions(LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile,
            string legacyConfigurationFile, bool mergeLegacyConfigIfExists)
        {
            ArgsLauncherConfiguration = argsLauncherConfiguration;
            LauncherConfigurationFile = launcherConfigurationFile;
            LegacyConfigurationFile = legacyConfigurationFile;
            MergeLegacyConfigIfExists = mergeLegacyConfigIfExists;
        }

        public LauncherConfiguration ArgsLauncherConfiguration { get; set; }
        public string LauncherConfigurationFile { get; set; }
        public string LegacyConfigurationFile { get; set; }
        public bool MergeLegacyConfigIfExists { get; set; }
    }

    public record CommonProperties
    {
        public CommonProperties(LauncherConfiguration launcherConfiguration,
            ftCashBoxConfiguration cashboxConfiguration, ECDiffieHellman clientEcdh,
            IDataProtectionProvider dataProtectionProvider)
        {
            LauncherConfiguration = launcherConfiguration;
            CashboxConfiguration = cashboxConfiguration;
            ClientEcdh = clientEcdh;
            DataProtectionProvider = dataProtectionProvider;
        }

        public LauncherConfiguration LauncherConfiguration { get; set; }
        public ftCashBoxConfiguration CashboxConfiguration { get; set; }
        public ECDiffieHellman ClientEcdh { get; set; }
        public IDataProtectionProvider DataProtectionProvider { get; set; }
    }

    public static class CommonHandler
    {
        public static async Task<int> HandleAsync<O, S>(
            CommonOptions options,
            O specificOptions,
            IHost host,
            Func<CommonOptions, CommonProperties, O, S, Task<int>> handler) where S : notnull
        {
            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            var launcherConfiguration = new LauncherConfiguration();

            Log.Verbose("Reading launcher config file.");
            try
            {
                options.LauncherConfigurationFile = Path.GetFullPath(options.LauncherConfigurationFile);
                launcherConfiguration =
                    LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(options.LauncherConfigurationFile));
            }
            catch (Exception e)
            {
                if (!(options.MergeLegacyConfigIfExists && File.Exists(options.LegacyConfigurationFile)))
                {
                    if (File.Exists(options.LauncherConfigurationFile))
                    {
                        Log.Warning(e, "Could not parse launcher configuration file \"{LauncherConfigurationFile}\".",
                            options.LauncherConfigurationFile);
                    }
                    else
                    {
                        Log.Warning("Launcher configuration file \"{LauncherConfigurationFile}\" does not exist.",
                            options.LauncherConfigurationFile);
                    }

                    Log.Warning("Using command line parameters only.", options.LauncherConfigurationFile);
                }
            }

            Log.Verbose("Merging legacy launcher config file.");
            if (options.MergeLegacyConfigIfExists && File.Exists(options.LegacyConfigurationFile))
            {
                var legacyConfig = await LegacyConfigFileReader.ReadLegacyConfigFile(options.LegacyConfigurationFile);
                launcherConfiguration.OverwriteWith(legacyConfig);

                var configFileDirectory = Path.GetDirectoryName(Path.GetFullPath(options.LauncherConfigurationFile));
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }

                await File.WriteAllTextAsync(options.LauncherConfigurationFile, legacyConfig.Serialize());

                var fi = new FileInfo(options.LegacyConfigurationFile);
                fi.CopyTo(options.LegacyConfigurationFile + ".legacy");
                fi.Delete();
            }

            Log.Verbose("Merging launcher cli args.");
            launcherConfiguration.OverwriteWith(options.ArgsLauncherConfiguration);

            if (!launcherConfiguration.UseOffline!.Value &&
                (launcherConfiguration.CashboxId is null || launcherConfiguration.AccessToken is null))
            {
                Log.Error("CashBoxId and AccessToken are not provided.");
            }

            try
            {
                var configFileDirectory = Path.GetDirectoryName(launcherConfiguration.CashboxConfigurationFile);
                if (configFileDirectory is not null)
                {
                    Directory.CreateDirectory(configFileDirectory);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not create cashbox-configuration-file folder.");
            }

            ECDiffieHellman? clientEcdh = null;
            try
            {
                clientEcdh = await LoadCurve(launcherConfiguration.CashboxId!.Value, launcherConfiguration.AccessToken!,
                    launcherConfiguration.ServiceFolder!, launcherConfiguration.UseOffline!.Value);
                using var downloader = new ConfigurationDownloader(launcherConfiguration);
                var exists = await downloader.DownloadConfigurationAsync(clientEcdh);
                if (launcherConfiguration.UseOffline!.Value && !exists)
                {
                    Log.Warning("Cashbox configuration was not downloaded because UseOffline is set.");
                }
            }
            catch (Exception e)
            {
                var message = "Could not download Cashbox configuration. ";
                message +=
                    $"(Launcher is running in {(launcherConfiguration.Sandbox!.Value ? "sandbox" : "production")} mode.";
                if (!launcherConfiguration.Sandbox!.Value)
                {
                    message += " Did you forget the --sandbox flag?";
                }

                message += ")";
                Log.Error(e, message);
            }

            try
            {
                var cashboxConfigurationFile = launcherConfiguration.CashboxConfigurationFile!;
                launcherConfiguration.OverwriteWith(
                    LauncherConfigurationInCashBoxConfiguration.Deserialize(
                        await File.ReadAllTextAsync(cashboxConfigurationFile)));
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not read Cashbox configuration file.");
            }

            var cashboxConfiguration = new ftCashBoxConfiguration();
            try
            {
                cashboxConfiguration =
                    CashBoxConfigurationExt.Deserialize(
                        await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));
                cashboxConfiguration.Decrypt(launcherConfiguration, clientEcdh);
            }
            catch (Exception e)
            {
                Log.Fatal(e, "Could not parse Cashbox configuration.");
            }

            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration)
                .AddFileLoggingConfiguration(launcherConfiguration,
                    new[] { "fiskaltrust.Launcher", launcherConfiguration.CashboxId?.ToString() })
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

            Log.Debug("Launcher Configuration File: {LauncherConfigurationFile}", options.LauncherConfigurationFile);
            Log.Debug("Cashbox Configuration File: {CashboxConfigurationFile}",
                launcherConfiguration.CashboxConfigurationFile);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", launcherConfiguration.Redacted());

            var dataProtectionProvider = DataProtectionExtensions.Create(launcherConfiguration.AccessToken,
                useFallback: launcherConfiguration.UseLegacyDataProtection!.Value);

            try
            {
                launcherConfiguration.Decrypt
                    (dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE));
            }
            catch (Exception e)
            {
                Log.Warning(e,
                    "Error decrypting launcher configuration. Please check your configuration settings. If necessary, use 'config set' command to update your configuration.");

                var serviceFolder = launcherConfiguration.ServiceFolder!;
                var cashboxId = launcherConfiguration.CashboxId!.Value;

                var dataProtector =
                    dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

                clientEcdh = CashboxConfigEncryption.CreateCurve();
                var clientEcdhPath = Path.Combine(serviceFolder, $"client-{cashboxId}.ecdh");
                await File.WriteAllTextAsync(clientEcdhPath, dataProtector.Protect(clientEcdh.Serialize()));

                using var downloader = new ConfigurationDownloader(launcherConfiguration);
                var exists = await downloader.DownloadConfigurationAsync(clientEcdh);
                if (!exists)
                {
                    throw new InvalidOperationException("Failed to download cashbox configuration.");
                }

                cashboxConfiguration =
                    CashBoxConfigurationExt.Deserialize(
                        await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));
                cashboxConfiguration.Decrypt(launcherConfiguration, clientEcdh);
            }

            return await handler(options,
                new CommonProperties(launcherConfiguration, cashboxConfiguration, clientEcdh, dataProtectionProvider),
                specificOptions, host.Services.GetRequiredService<S>());
        }

        public static async Task<ECDiffieHellman> LoadCurve(Guid cashboxId, string accessToken, string serviceFolder,
            bool useOffline = false, bool dryRun = false, bool useFallback = false)
        {
            Log.Verbose("Loading Curve.");
            var dataProtector = DataProtectionExtensions.Create(accessToken, useFallback: useFallback)
                .CreateProtector(CashBoxConfigurationExt.DATA_PROTECTION_DATA_PURPOSE);
            var clientEcdhPath = Path.Combine(serviceFolder, $"client-{cashboxId}.ecdh");

            try
            {
                if (File.Exists(clientEcdhPath))
                {
                    return ECDiffieHellmanExt.Deserialize(
                        dataProtector.Unprotect(await File.ReadAllTextAsync(clientEcdhPath)));
                }
            }
            catch (Exception e)
            {
                Log.Warning($"Error loading or decrypting ECDH curve: {e.Message}. Regenerating new curve.");
            }

            const string offlineClientEcdhPath = "/client.ecdh";
            if (!dryRun && useOffline && File.Exists(offlineClientEcdhPath))
            {
                var clientEcdh = ECDiffieHellmanExt.Deserialize(await File.ReadAllTextAsync(offlineClientEcdhPath));
                try
                {
                    File.Delete(offlineClientEcdhPath);
                }
                catch
                {
                }

                return clientEcdh;
            }

            // Regenerating the curve if it's not loaded or in case of an error
            var newClientEcdh = CashboxConfigEncryption.CreateCurve();
            if (!dryRun)
            {
                await File.WriteAllTextAsync(clientEcdhPath, dataProtector.Protect(newClientEcdh.Serialize()));
            }

            return newClientEcdh;
        }
    }
}