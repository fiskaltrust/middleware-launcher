using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Constants;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = fiskaltrust.Launcher.Common.Extensions.LoggerExtensions;

namespace fiskaltrust.Launcher.Commands
{

    public class ConfigCommand : Command
    {
        public ConfigCommand() : base("config")
        {
            AddOption(new Option<string?>("--launcher-version"));

            AddOption(new Option<Guid?>("--cashbox-id"));
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<bool>("--sandbox"));
            AddOption(new Option<string?>("--log-folder"));

            var logLevelOption = new Option<LogLevel?>("--log-level", "Set the log level of the application.");
            logLevelOption.AddAlias("-v");
            logLevelOption.AddAlias("--verbosity");
            AddOption(logLevelOption);

            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => Paths.LauncherConfigurationFileName));
            AddOption(new Option<string>("--legacy-configuration-file", getDefaultValue: () => Paths.LegacyConfigurationFileName));

            AddCommand(new ConfigSetCommand()
            {
                Handler = CommandHandler.Create<ConfigSetOptions>(ConfigSetHandler.HandleAsync)
            });
            AddCommand(new ConfigGetCommand()
            {
                Handler = CommandHandler.Create<ConfigGetOptions>(ConfigGetHandler.HandleAsync)
            });
        }
    }

    public class ConfigSetCommand : RunCommand
    {
        public ConfigSetCommand() : base("set", false)
        {
            AddOption(new Option<SemanticVersioning.Range?>("--launcher-version", parseArgument: arg => SemanticVersioning.Range.Parse(arg.Tokens.Single().Value)));
        }
    }

    public class ConfigSetOptions
    {
        public ConfigSetOptions(LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile)
        {
            ArgsLauncherConfiguration = argsLauncherConfiguration;
            LauncherConfigurationFile = launcherConfigurationFile;
        }

        public LauncherConfiguration ArgsLauncherConfiguration { get; set; }
        public string LauncherConfigurationFile { get; set; }
    }

    public static class ConfigSetHandler
    {
        public static async Task<int> HandleAsync(ConfigSetOptions configSetOptions)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            var logger = Serilog.Log.Logger.ToDotnetLogger();

            LauncherConfiguration launcherConfiguration;
            string rawLauncherConfigurationOld = "{\n}";

            IDataProtector dataProtector;
            if (!File.Exists(configSetOptions.LauncherConfigurationFile))
            {
                if (configSetOptions.ArgsLauncherConfiguration.AccessToken is null)
                {
                    logger.LauncherConfigFileNotExist(configSetOptions.LauncherConfigurationFile);
                    logger.SpecifyAccessTokenParameter();
                    return 1;
                }

                logger.LauncherConfigFileNotExistCreating(configSetOptions.LauncherConfigurationFile);
                launcherConfiguration = new LauncherConfiguration();

                dataProtector = DataProtectionExtensions.Create(configSetOptions.ArgsLauncherConfiguration).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);
            }
            else
            {
                try
                {
                    launcherConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(configSetOptions.LauncherConfigurationFile));
                }
                catch (Exception e)
                {
                    logger.CouldNotReadLauncherConfigGeneral(e);
                    return 1;
                }

                if (configSetOptions.ArgsLauncherConfiguration.AccessToken is null && launcherConfiguration?.AccessToken is null)
                {
                    logger.SpecifyAccessTokenInConfig();
                    return 1;
                }

                dataProtector = DataProtectionExtensions.Create(configSetOptions.ArgsLauncherConfiguration).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

                try
                {
                    launcherConfiguration!.Decrypt(dataProtector);
                }
                catch (Exception e)
                {
                    logger.ErrorDecryptingLauncherConfigFile(e);
                }

                try
                {
                    rawLauncherConfigurationOld = launcherConfiguration!.Serialize(true, true);
                }
                catch (Exception e)
                {
                    logger.ErrorReserialisingLauncherConfig(e);
                    return 1;
                }
            }

            launcherConfiguration.OverwriteWith(configSetOptions.ArgsLauncherConfiguration);

            string rawLauncherConfigurationNew;
            rawLauncherConfigurationNew = launcherConfiguration.Serialize(true, true);

            try
            {
                launcherConfiguration.Encrypt(dataProtector);
            }
            catch (Exception e)
            {
                logger.ErrorEncryptingLauncherConfig(e);
            }

            try
            {
                await File.WriteAllTextAsync(configSetOptions.LauncherConfigurationFile, launcherConfiguration.Serialize(true, true));
            }
            catch (Exception e)
            {
                logger.CouldNotWriteLauncherConfig(e);
                return 1;
            }

            logger.SetValuesInLauncherConfig(configSetOptions.LauncherConfigurationFile);

            var diff = InlineDiffBuilder.Diff(rawLauncherConfigurationOld, rawLauncherConfigurationNew);
            var savedColor = Console.ForegroundColor;
            foreach (var line in diff.Lines)
            {
                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("+ ");
                        break;
                    case ChangeType.Deleted:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("- ");
                        break;
                    default:
                        Console.ForegroundColor = savedColor;
                        Console.Write("  ");
                        break;
                }

                Console.WriteLine(line.Text);
            }
            Console.ForegroundColor = savedColor;

            return 0;
        }
    }

    public class ConfigGetCommand : Command
    {
        public ConfigGetCommand() : base("get")
        {
            AddOption(new Option<string?>("--access-token"));
            AddOption(new Option<string?>("--cashbox-configuration-file"));
        }
    }

    public class ConfigGetOptions
    {
        public string? AccessToken { get; set; }
        public string? LauncherConfigurationFile { get; set; }
        public string? LegacyConfigurationFile { get; set; }
        public string? CashBoxConfigurationFile { get; set; }
    }

    public static class ConfigGetHandler
    {
        public static async Task<int> HandleAsync(ConfigGetOptions configGetOptions)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            var logger = Serilog.Log.Logger.ToDotnetLogger();

            LauncherConfiguration? localConfiguration = null;
            if (configGetOptions.LauncherConfigurationFile is not null)
            {
                localConfiguration = await ReadLauncherConfiguration(configGetOptions.LauncherConfigurationFile, configGetOptions.AccessToken, LauncherConfiguration.Deserialize, logger);

                if (localConfiguration is not null)
                {
                    logger.LocalConfigurationInfo(configGetOptions.LauncherConfigurationFile!, localConfiguration.Serialize(true, true));
                }
            }

            if (configGetOptions.LegacyConfigurationFile is not null && File.Exists(configGetOptions.LegacyConfigurationFile))
            {
                LauncherConfiguration? legacyConfiguration = await LegacyConfigFileReader.ReadLegacyConfigFile(configGetOptions.LegacyConfigurationFile);

                if (legacyConfiguration is not null)
                {
                    logger.LegacyConfigurationInfo(configGetOptions.LegacyConfigurationFile!, legacyConfiguration.Serialize(true, true));
                }
            }

            configGetOptions.CashBoxConfigurationFile ??= localConfiguration?.CashboxConfigurationFile;
            if (configGetOptions.CashBoxConfigurationFile is not null && File.Exists(configGetOptions.CashBoxConfigurationFile))
            {
                LauncherConfiguration? remoteConfiguration = await ReadLauncherConfiguration(configGetOptions.CashBoxConfigurationFile, configGetOptions.AccessToken, LauncherConfigurationInCashBoxConfiguration.Deserialize, logger);

                if (remoteConfiguration is not null)
                {
                    logger.RemoteConfigurationInfo(configGetOptions.CashBoxConfigurationFile!, remoteConfiguration.Serialize(true, true));
                }
            }

            return 0;
        }

        public static async Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, string? accessToken, Func<string, Task<LauncherConfiguration?>> deserialize, ILogger logger)
        {
            LauncherConfiguration? launcherConfiguration = null;
            try
            {
                launcherConfiguration = await deserialize(await File.ReadAllTextAsync(launcherConfigurationFile));
            }
            catch (Exception e)
            {
                logger.CouldNotReadLauncherConfig(e, launcherConfigurationFile);
            }

            if (launcherConfiguration is null)
            {
                return null;
            }

            if (accessToken is null && launcherConfiguration!.AccessToken is null)
            {
                logger.ToDecryptSpecifyAccessToken();
            }
            else
            {
                var dataProtector = DataProtectionExtensions.Create(launcherConfiguration).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

                try
                {
                    launcherConfiguration!.Decrypt(dataProtector);
                }
                catch (Exception e)
                {
                    logger.ErrorDecryptingLauncherConfig(e, launcherConfigurationFile);
                }
            }

            return launcherConfiguration;
        }

        public static Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, string? accessToken, Func<string, LauncherConfiguration?> deserialize, ILogger logger) => ReadLauncherConfiguration(launcherConfigurationFile, accessToken, (content) => Task.FromResult(deserialize(content)), logger);
    }
}
