using System.CommandLine;
using System.CommandLine.Invocation;
using System.Runtime.InteropServices;
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
using fiskaltrust.Launcher.ServiceInstallation;
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
                AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => Paths.LauncherConfigurationFileName));
                AddOption(new Option<string>("--legacy-configuration-file", getDefaultValue: () => Paths.LegacyConfigurationFileName));
                AddOption(new Option<bool>("--merge-legacy-config-if-exists", getDefaultValue: () => true));
            }
        }
    }

    public class CommonOptions
    {
        public CommonOptions(LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile, string legacyConfigurationFile, bool mergeLegacyConfigIfExists)
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
        public CommonProperties(LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashboxConfiguration, ECDiffieHellman clientEcdh, IDataProtectionProvider dataProtectionProvider)
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
            // Log messages will be save here and logged later when we have the configuration options to create the logger.
            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            var launcherConfiguration = new LauncherConfiguration();

            Log.Verbose("Reading launcher config file.");
            try
            {
                options.LauncherConfigurationFile = Path.GetFullPath(options.LauncherConfigurationFile);
                launcherConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(options.LauncherConfigurationFile));
            }
            catch (Exception e)
            {
                if (!(options.MergeLegacyConfigIfExists && File.Exists(options.LegacyConfigurationFile)))
                {
                    if (File.Exists(options.LauncherConfigurationFile))
                    {
                        Log.Warning(e, "Could not parse launcher configuration file \"{LauncherConfigurationFile}\".", options.LauncherConfigurationFile);
                    }
                    else
                    {
                        Log.Warning("Launcher configuration file \"{LauncherConfigurationFile}\" does not exist.", options.LauncherConfigurationFile);
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
            await EnsureServiceDirectoryExists(launcherConfiguration);

            if (!launcherConfiguration.UseOffline!.Value && (launcherConfiguration.CashboxId is null || launcherConfiguration.AccessToken is null))
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
                clientEcdh = await LoadCurve(launcherConfiguration.CashboxId!.Value, launcherConfiguration.AccessToken!, launcherConfiguration.ServiceFolder!, launcherConfiguration.UseOffline!.Value,  useFallback: launcherConfiguration.UseLegacyDataProtection!.Value);
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
                message += $"(Launcher is running in {(launcherConfiguration.Sandbox!.Value ? "sandbox" : "production")} mode.";
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
                launcherConfiguration.OverwriteWith(LauncherConfigurationInCashBoxConfiguration.Deserialize(await File.ReadAllTextAsync(cashboxConfigurationFile)));
            }
            catch (Exception e)
            {
                // will exit with non-zero exit code later.
                Log.Fatal(e, "Could not read Cashbox configuration file.");
            }

            var cashboxConfiguration = new ftCashBoxConfiguration();
            try
            {
                cashboxConfiguration = CashBoxConfigurationExt.Deserialize(await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));
                cashboxConfiguration.Decrypt(launcherConfiguration, clientEcdh);
            }
            catch (Exception e)
            {
                // will exit with non-zero exit code later.
                Log.Fatal(e, "Could not parse Cashbox configuration.");
            }

            // Previous log messages will be logged here using this logger.
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration)
                .AddFileLoggingConfiguration(launcherConfiguration, new[] { "fiskaltrust.Launcher", launcherConfiguration.CashboxId?.ToString() })
                .Enrich.FromLogContext()
                .CreateLogger();

            foreach (var logEvent in collectionSink.Events)
            {
                Log.Write(logEvent);
            }

            // If any critical errors occured, we exit with a non-zero exit code.
            // In many cases we don't want to immediately exit the application,
            // but we want to log the error and continue and see what else is going on before we exit.
            if (collectionSink.Events.Where(e => e.Level == LogEventLevel.Fatal).Any())
            {
                return 1;
            }

            Log.Debug("Launcher Configuration File: {LauncherConfigurationFile}", options.LauncherConfigurationFile);
            Log.Debug("Cashbox Configuration File: {CashboxConfigurationFile}", launcherConfiguration.CashboxConfigurationFile);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", launcherConfiguration.Redacted());


            var dataProtectionProvider = DataProtectionExtensions.Create(launcherConfiguration.AccessToken, useFallback: launcherConfiguration.UseLegacyDataProtection!.Value);

            try
            {
                launcherConfiguration.Decrypt(dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE));
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error decrypring launcher configuration file.");
            }

            return await handler(options, new CommonProperties(launcherConfiguration, cashboxConfiguration, clientEcdh, dataProtectionProvider), specificOptions, host.Services.GetRequiredService<S>());
        }

        private static async Task EnsureServiceDirectoryExists(LauncherConfiguration config)
        {
            var serviceDirectory = config.ServiceFolder;
            try
            {
                if (!Directory.Exists(serviceDirectory))
                {
                    Directory.CreateDirectory(serviceDirectory);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var user = Environment.GetEnvironmentVariable("USER");
                        if (!string.IsNullOrEmpty(user))
                        {
                            var chownResult = await ProcessHelper.RunProcess("chown", new[] { user, serviceDirectory }, LogEventLevel.Debug);
                            if (chownResult.exitCode != 0)
                            {
                                Log.Warning("Failed to change owner of the service directory.");
                            }

                            var chmodResult = await ProcessHelper.RunProcess("chmod", new[] { "774", serviceDirectory }, LogEventLevel.Debug);
                            if (chmodResult.exitCode != 0)
                            {
                                Log.Warning("Failed to change permissions of the service directory.");
                            }
                        }
                        else
                        {
                            Log.Warning("Service user name is not set. Owner of the service directory will not be changed.");
                        }
                    }
                    else
                    {
                        Log.Debug("Changing owner and permissions is skipped on non-Unix operating systems.");
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                // will exit with non-zero exit code later.
                Log.Fatal(e, "Access to the path '{ServiceDirectory}' is denied. Please run the application with sufficient permissions.", serviceDirectory);
            }
        }

        public static async Task<ECDiffieHellman> LoadCurve(Guid cashboxId, string accessToken, string serviceFolder, bool useOffline = false, bool dryRun = false, bool useFallback = false)
        {
            Log.Verbose("Loading Curve.");
            var dataProtector = DataProtectionExtensions.Create(accessToken, useFallback: useFallback).CreateProtector(CashBoxConfigurationExt.DATA_PROTECTION_DATA_PURPOSE);
            var clientEcdhPath = Path.Combine(serviceFolder, $"client-{cashboxId}.ecdh");

            if (File.Exists(clientEcdhPath))
            {
                try
                {
                    return ECDiffieHellmanExt.Deserialize(dataProtector.Unprotect(await File.ReadAllTextAsync(clientEcdhPath)));
                }
                catch (Exception e)
                {
                    Log.Warning($"Error loading or decrypting ECDH curve: {e.Message}. Regenerating new curve.");
                }
            }

            // Handling offline client ECDH path
            const string offlineClientEcdhPath = "/client.ecdh";
            if (!dryRun && useOffline && File.Exists(offlineClientEcdhPath))
            {
                var clientEcdh = ECDiffieHellmanExt.Deserialize(await File.ReadAllTextAsync(offlineClientEcdhPath));
                try
                {
                    File.Delete(offlineClientEcdhPath);
                }
                catch { }

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