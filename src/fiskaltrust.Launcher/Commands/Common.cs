using System.CommandLine;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = fiskaltrust.Launcher.Common.Extensions.LoggerExtensions;

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
            }
        }
    }

    public class CommonOptions
    {
        public CommonOptions(LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile,
            string legacyConfigurationFile)
        {
            ArgsLauncherConfiguration = argsLauncherConfiguration;
            LauncherConfigurationFile = launcherConfigurationFile;
            LegacyConfigurationFile = legacyConfigurationFile;
        }

        public LauncherConfiguration ArgsLauncherConfiguration { get; set; }
        public string LauncherConfigurationFile { get; set; }
        public string LegacyConfigurationFile { get; set; }
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
            // Log messages will be saved here and logged later when we have the configuration options to create the logger.
            var collectionSink = new CollectionSink();
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(collectionSink)
                .CreateLogger();

            var logger = Log.Logger.ToDotnetLogger();

            var launcherConfiguration = await LauncherConfiguration.ReadFromFilesAsync(options.LauncherConfigurationFile, options.LegacyConfigurationFile);

            Log.Verbose("Merging launcher cli args.");
            launcherConfiguration.OverwriteWith(options.ArgsLauncherConfiguration);

            await EnsureServiceDirectoryExists(launcherConfiguration, logger);

            if (!launcherConfiguration.UseOffline!.Value &&
                (launcherConfiguration.CashboxId is null || launcherConfiguration.AccessToken is null))
            {
                logger.CashboxIdAndAccessTokenNotProvided();
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
                logger.CouldNotCreateCashboxConfigFolder(e);
            }

            ECDiffieHellman? clientEcdh = null;
            try
            {
                clientEcdh = await LoadCurve(launcherConfiguration, logger);
            }
            catch (Exception e)
            {
                logger.CouldNotLoadClientCurve(e);
            }

            try
            {
                if (clientEcdh is not null)
                {
                    using var downloader = new ConfigurationDownloader(launcherConfiguration);
                    var exists = await downloader.DownloadConfigurationAsync(clientEcdh);
                    if (launcherConfiguration.UseOffline!.Value && !exists)
                    {
                        logger.CashboxConfigNotDownloadedOfflineMode();
                    }
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
                logger.CouldNotDownloadCashboxConfig(e, message);
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
                // will exit with non-zero exit code later.
                logger.CouldNotReadCashboxConfig(e);
            }
            launcherConfiguration.LogConfigurationWarnings(logger);
            var cashboxConfiguration = new ftCashBoxConfiguration();
            try
            {
                cashboxConfiguration =
                    CashBoxConfigurationExt.Deserialize(
                        await File.ReadAllTextAsync(launcherConfiguration.CashboxConfigurationFile!));
                if (clientEcdh is not null)
                {
                    cashboxConfiguration.Decrypt(launcherConfiguration, clientEcdh);
                }
            }
            catch (Exception e)
            {
                // will exit with non-zero exit code later.
                logger.CouldNotParseCashboxConfig(e);
            }

            // Previous log messages will be logged here using this logger.
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration(launcherConfiguration)
                .AddFileLoggingConfiguration(launcherConfiguration,
                    new[] { "fiskaltrust.Launcher", launcherConfiguration.CashboxId?.ToString() })
                .Enrich.FromLogContext()
                .CreateLogger();

            logger = Log.Logger.ToDotnetLogger();

            foreach (var logEvent in collectionSink.Events)
            {
                Log.Write(logEvent);
            }

            // If any critical errors occurred, we exit with a non-zero exit code.
            // In many cases we don't want to immediately exit the application,
            // but we want to log the error and continue and see what else is going on before we exit.
            if (collectionSink.Events.Where(e => e.Level == LogEventLevel.Fatal).Any())
            {
                return 1;
            }

            logger.LauncherConfigFileDebug(options.LauncherConfigurationFile);
            logger.CashboxConfigFileDebug(launcherConfiguration.CashboxConfigurationFile!);
            Log.Debug("Launcher Configuration: {@LauncherConfiguration}", launcherConfiguration.Redacted());

            logger.LauncherRunningAsServiceType(
                Enum.GetName(typeof(ServiceTypes), host.Services.GetRequiredService<ServiceType>().Type)!);

            var dataProtectionProvider = DataProtectionExtensions.Create(launcherConfiguration);

            try
            {
                launcherConfiguration.Decrypt(
                    dataProtectionProvider.CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE));
            }
            catch (Exception e)
            {
                logger.ErrorDecryptingLauncherConfig(e, options.LauncherConfigurationFile);
            }

            return await handler(options,
                new CommonProperties(launcherConfiguration, cashboxConfiguration, clientEcdh!, dataProtectionProvider),
                specificOptions, host.Services.GetRequiredService<S>());
        }

        private static async Task EnsureServiceDirectoryExists(LauncherConfiguration config, ILogger logger)
        {
            var serviceDirectory = config.ServiceFolder!;
            try
            {
                if (!Directory.Exists(serviceDirectory))
                {
                    Directory.CreateDirectory(serviceDirectory);

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                        RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        var user = Environment.GetEnvironmentVariable("USER");
                        if (!string.IsNullOrEmpty(user))
                        {
                            var chownResult = await ProcessHelper.RunProcess("chown", [user, $"\"{serviceDirectory}\""],
                                LogEventLevel.Debug);
                            if (chownResult.exitCode != 0)
                            {
                                logger.FailedToChangeOwnerOfServiceDirectory();
                            }

                            var chmodResult = await ProcessHelper.RunProcess("chmod", ["774", $"\"{serviceDirectory}\""],
                                LogEventLevel.Debug);
                            if (chmodResult.exitCode != 0)
                            {
                                logger.FailedToChangePermissionsOfServiceDirectory();
                            }
                        }
                        else
                        {
                            logger.ServiceUserNameNotSet();
                        }
                    }
                    else
                    {
                        logger.ChangingOwnerAndPermissionsSkipped();
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                // will exit with non-zero exit code later.
                logger.CouldNotCreateServiceDirectory(e, $"Access to the path '{serviceDirectory}' is denied. Please run the application with sufficient permissions.");
            }
        }

        public static async Task<ECDiffieHellman> LoadCurve(LauncherConfiguration launcherConfiguration, ILogger? logger = null, bool dryRun = false)
        {
            logger ??= Serilog.Log.Logger.ToDotnetLogger();

            Log.Verbose("Loading Curve.");
            var dataProtector = DataProtectionExtensions.Create(launcherConfiguration)
                .CreateProtector(CashBoxConfigurationExt.DATA_PROTECTION_DATA_PURPOSE);
            var clientEcdhPath = Path.Combine(launcherConfiguration.ServiceFolder!, $"client-{launcherConfiguration.CashboxId!.Value}.ecdh");

            ECDiffieHellman? clientEcdh = null;

            if (File.Exists(clientEcdhPath))
            {
                try
                {
                    clientEcdh = ECDiffieHellmanExt.Deserialize(
                        dataProtector.Unprotect(await File.ReadAllTextAsync(clientEcdhPath)));
                }
                catch (Exception e)
                {
                    logger.ErrorLoadingEcdhCurveRegenerating(e.Message);
                }
            }

            // Handling offline client ECDH path
            const string offlineClientEcdhPath = "/client.ecdh";
            if (!dryRun && launcherConfiguration.UseOffline!.Value && File.Exists(offlineClientEcdhPath) && clientEcdh == null)
            {
                clientEcdh = ECDiffieHellmanExt.Deserialize(await File.ReadAllTextAsync(offlineClientEcdhPath));
                try
                {
                    File.Delete(offlineClientEcdhPath);
                }
                catch (Exception e)
                {
                    logger.ErrorLoadingEcdhCurve(e, clientEcdhPath);
                    throw;
                }
            }

            if (clientEcdh == null)
            {
                // Regenerating the curve if it's not loaded or in case of an error
                clientEcdh = CashboxConfigEncryption.CreateCurve();
                if (!dryRun)
                {
                    await File.WriteAllTextAsync(clientEcdhPath, dataProtector.Protect(clientEcdh.Serialize()));
                }
            }

            return clientEcdh;
        }
    }
}
