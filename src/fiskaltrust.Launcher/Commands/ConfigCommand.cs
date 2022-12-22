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

namespace fiskaltrust.Launcher.Commands
{

    public class ConfigCommand : Command
    {
        public ConfigCommand() : base("config")
        {
            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "launcher.configuration.json"));
            AddCommand(new ConfigSetCommand());
            AddCommand(new ConfigGetCommand());
        }
    }

    public class ConfigSetCommand : RunCommand
    {
        public ConfigSetCommand() : base("set", false)
        {
            AddOption(new Option<SemanticVersioning.Range?>("--launcher-version", parseArgument: arg => SemanticVersioning.Range.Parse(arg.Tokens.Single().Value)));
        }
    }

    public class ConfigSetCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            LauncherConfiguration launcherConfiguration;
            string rawLauncherConfigurationOld = "{\n}";

            IDataProtector dataProtector;
            if (!File.Exists(LauncherConfigurationFile))
            {
                if (ArgsLauncherConfiguration.AccessToken is null)
                {
                    Log.Warning("Launcher configuration file {file} does not exist.", LauncherConfigurationFile);
                    Log.Error("Please specify the --access-token parameter or an existing launcher configuration file containing an access token.");
                    return 1;
                }

                Log.Warning("Launcher configuration file {file} does not exist. Creating new file.", LauncherConfigurationFile);
                launcherConfiguration = new LauncherConfiguration();

                dataProtector = DataProtectionExtensions.Create(ArgsLauncherConfiguration.AccessToken).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);
            }
            else
            {
                try
                {
                    launcherConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(LauncherConfigurationFile));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not read launcher configuration");
                    return 1;
                }

                if (ArgsLauncherConfiguration.AccessToken is null && launcherConfiguration?.AccessToken is null)
                {
                    Log.Error("Please specify the --access-token parameter or set it in the provided launcher configuration file.");
                    return 1;
                }

                dataProtector = DataProtectionExtensions.Create(ArgsLauncherConfiguration.AccessToken ?? launcherConfiguration?.AccessToken).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

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

            launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);
            launcherConfiguration.DisableDefaults();

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
                await File.WriteAllTextAsync(LauncherConfigurationFile, launcherConfiguration.Serialize(true));
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not write launcher configuration");
                return 1;
            }

            Log.Information("Set values in launcher configuration file {file}.", LauncherConfigurationFile);

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

    public class ConfigGetCommandHandler : ICommandHandler
    {
        public string? AccessToken { get; set; }
        public string? LauncherConfigurationFile { get; set; }
        public string? LegacyConfigFile { get; set; }
        public string? CashBoxConfigurationFile { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            LauncherConfiguration? localConfiguration = null;
            if (LauncherConfigurationFile is not null)
            {
                localConfiguration = await ReadLauncherConfiguration(LauncherConfigurationFile, LauncherConfiguration.Deserialize);

                if (localConfiguration is not null)
                {
                    Log.Information($"Local configuration {{LauncherConfigurationFile}}\n{localConfiguration.Serialize(true, true)}", LauncherConfigurationFile);
                }
            }

            if (LegacyConfigFile is not null)
            {
                LauncherConfiguration? legacyConfiguration = await ReadLauncherConfiguration(LegacyConfigFile, LegacyConfigFileReader.ReadLegacyConfigFile!);

                if (legacyConfiguration is not null)
                {
                    Log.Information($"Legacy configuration {{LegacyConfigFile}}\n{legacyConfiguration.Serialize(true, true)}", LegacyConfigFile);
                }
            }

            CashBoxConfigurationFile ??= localConfiguration?.CashboxConfigurationFile;
            if (CashBoxConfigurationFile is not null)
            {
                LauncherConfiguration? remoteConfiguration = await ReadLauncherConfiguration(CashBoxConfigurationFile, LauncherConfigurationInCashBoxConfiguration.Deserialize);

                if (remoteConfiguration is not null)
                {
                    Log.Information($"Remote configuration from {{CashBoxConfigurationFile}}\n{remoteConfiguration.Serialize(true, true)}", CashBoxConfigurationFile);
                }
            }

            return 0;
        }

        public async Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, Func<string, Task<LauncherConfiguration?>> deserialize)
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

            if (AccessToken is null && launcherConfiguration?.AccessToken is null)
            {
                Log.Warning("To decrypt the encrypted values from the configuration file specify the --access-token parameter or set it in the provided launcher configuration file.");
            }
            else
            {
                var dataProtector = DataProtectionExtensions.Create(AccessToken ?? launcherConfiguration?.AccessToken).CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

                try
                {
                    launcherConfiguration?.Decrypt(dataProtector);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error decrypting launcher configuration file {file}.", launcherConfigurationFile);
                }
            }

            launcherConfiguration?.DisableDefaults();

            return launcherConfiguration;
        }

        public Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, Func<string, LauncherConfiguration?> deserialize) => ReadLauncherConfiguration(launcherConfigurationFile, (content) => Task.FromResult(deserialize(content)));
    }
}
