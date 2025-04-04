using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Configuration;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using fiskaltrust.Launcher.Extensions;
using Microsoft.AspNetCore.DataProtection;
using System.CommandLine.NamingConventionBinder;

namespace fiskaltrust.Launcher.Commands
{

    public class ConfigCommand : Command
    {
        public ConfigCommand() : base("config")
        {
            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
            AddOption(new Option<string?>("--launcher-version"));

            var logLevelOption = new Option<LogLevel?>("--log-level", "Set the log level of the application.");
            logLevelOption.AddAlias("-v");
            logLevelOption.AddAlias("--verbosity");
            AddOption(logLevelOption);

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

            LauncherConfiguration launcherConfiguration;
            string rawLauncherConfigurationOld = "{\n}";

            IDataProtector dataProtector;
            if (!File.Exists(configSetOptions.LauncherConfigurationFile))
            {
                if (configSetOptions.ArgsLauncherConfiguration.AccessToken is null)
                {
                    Log.Warning("Launcher configuration file {file} does not exist.", configSetOptions.LauncherConfigurationFile);
                    Log.Error("Please specify the --access-token parameter or an existing launcher configuration file containing an access token.");
                    return 1;
                }

                Log.Warning("Launcher configuration file {file} does not exist. Creating new file.", configSetOptions.LauncherConfigurationFile);
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
                    Log.Error(e, "Could not read launcher configuration");
                    return 1;
                }

                if (configSetOptions.ArgsLauncherConfiguration.AccessToken is null && launcherConfiguration?.AccessToken is null)
                {
                    Log.Error("Please specify the --access-token parameter or set it in the provided launcher configuration file.");
                    return 1;
                }

                dataProtector = DataProtectionExtensions.Create(configSetOptions.ArgsLauncherConfiguration).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

                try
                {
                    launcherConfiguration!.Decrypt(dataProtector);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error decrypting launcher configuration file.");
                }

                try
                {
                    rawLauncherConfigurationOld = launcherConfiguration!.Serialize(true, true);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error reserializing launcher configuration file.");
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
                Log.Warning(e, "Error encrypting launcher configuration file.");
            }

            try
            {
                await File.WriteAllTextAsync(configSetOptions.LauncherConfigurationFile, launcherConfiguration.Serialize(true, true));
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not write launcher configuration");
                return 1;
            }

            Log.Information("Set values in launcher configuration file {file}.", configSetOptions.LauncherConfigurationFile);

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
            AddOption(new Option<string?>("--legacy-config-file"));
        }
    }

    public class ConfigGetOptions
    {
        public string? AccessToken { get; set; }
        public string? LauncherConfigurationFile { get; set; }
        public string? LegacyConfigFile { get; set; }
        public string? CashBoxConfigurationFile { get; set; }
    }

    public static class ConfigGetHandler
    {
        public static async Task<int> HandleAsync(ConfigGetOptions configGetOptions)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            LauncherConfiguration? localConfiguration = null;
            if (configGetOptions.LauncherConfigurationFile is not null)
            {
                localConfiguration = await ReadLauncherConfiguration(configGetOptions.LauncherConfigurationFile, configGetOptions.AccessToken, LauncherConfiguration.Deserialize);

                if (localConfiguration is not null)
                {
                    Log.Information($"Local configuration {{LauncherConfigurationFile}}\n{localConfiguration.Serialize(true, true)}", configGetOptions.LauncherConfigurationFile);
                }
            }

            if (configGetOptions.LegacyConfigFile is not null)
            {
                LauncherConfiguration? legacyConfiguration = await ReadLauncherConfiguration(configGetOptions.LegacyConfigFile, configGetOptions.AccessToken, LegacyConfigFileReader.ReadLegacyConfigFile!);

                if (legacyConfiguration is not null)
                {
                    Log.Information($"Legacy configuration {{LegacyConfigFile}}\n{legacyConfiguration.Serialize(true, true)}", configGetOptions.LegacyConfigFile);
                }
            }

            configGetOptions.CashBoxConfigurationFile ??= localConfiguration?.CashboxConfigurationFile;
            if (configGetOptions.CashBoxConfigurationFile is not null && File.Exists(configGetOptions.CashBoxConfigurationFile))
            {
                LauncherConfiguration? remoteConfiguration = await ConfigGetHandler.ReadLauncherConfiguration(configGetOptions.CashBoxConfigurationFile, configGetOptions.AccessToken, LauncherConfigurationInCashBoxConfiguration.Deserialize);

                if (remoteConfiguration is not null)
                {
                    Log.Information($"Remote configuration from {{CashBoxConfigurationFile}}\n{remoteConfiguration.Serialize(true, true)}", configGetOptions.CashBoxConfigurationFile);
                }
            }

            return 0;
        }

        public static async Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, string? accessToken, Func<string, Task<LauncherConfiguration?>> deserialize)
        {
            LauncherConfiguration? launcherConfiguration = null;
            try
            {
                launcherConfiguration = await deserialize(await File.ReadAllTextAsync(launcherConfigurationFile));
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not read launcher configuration {file}.", launcherConfigurationFile);
            }

            if (launcherConfiguration is null)
            {
                return null;
            }

            if (accessToken is null && launcherConfiguration!.AccessToken is null)
            {
                Log.Warning("To decrypt the encrypted values from the configuration file specify the --access-token parameter or set it in the provided launcher configuration file.");
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
                    Log.Warning(e, "Error decrypting launcher configuration file {file}.", launcherConfigurationFile);
                }
            }

            return launcherConfiguration;
        }

        public static Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, string? accessToken, Func<string, LauncherConfiguration?> deserialize) => ReadLauncherConfiguration(launcherConfigurationFile, accessToken, (content) => Task.FromResult(deserialize(content)));
    }
}
