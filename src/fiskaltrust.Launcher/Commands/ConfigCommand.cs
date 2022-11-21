using System.CommandLine;
using System.CommandLine.Invocation;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Configuration;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace fiskaltrust.Launcher.Commands
{

    public class ConfigCommand : Command
    {
        public ConfigCommand() : base("config")
        {
            AddOption(new Option<string?>("--cipher-access-token"));

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

        public string? CipherAccessToken { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            LauncherConfiguration launcherConfiguration;
            string rawLauncherConfigurationOld = "{\n}";

            if (!File.Exists(LauncherConfigurationFile))
            {
                Log.Warning("Launcher configuration file {file} does not exist. Creating new file.", LauncherConfigurationFile);
                launcherConfiguration = new LauncherConfiguration();
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

                try
                {
                    launcherConfiguration.Decrypt(CipherAccessToken);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error decrypting launcher configuration file.");
                }

                try
                {
                    rawLauncherConfigurationOld = launcherConfiguration.Serialize(true, true);
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
                launcherConfiguration.Encrypt(CipherAccessToken);
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
            if (diff.HasDifferences)
            {
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
            }

            return 0;
        }
    }

    public class ConfigGetCommand : Command
    {
        public ConfigGetCommand() : base("get")
        {
            AddOption(new Option<string?>("--cashbox-configuration-file"));
            AddOption(new Option<string?>("--legacy-config-file"));
        }
    }

    public class ConfigGetCommandHandler : ICommandHandler
    {
        public string? LauncherConfigurationFile { get; set; }
        public string? LegacyConfigFile { get; set; }
        public string? CashBoxConfigurationFile { get; set; }

        public string? CipherAccessToken { get; set; }

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
            LauncherConfiguration? configuration = null;
            try
            {
                configuration = await deserialize(await File.ReadAllTextAsync(launcherConfigurationFile));
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not read launcher configuration {file}.", launcherConfigurationFile);
            }

            try
            {
                configuration?.Decrypt(CipherAccessToken);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error decrypting launcher configuration file {file}.", launcherConfigurationFile);
            }
            configuration?.DisableDefaults();

            return configuration;
        }

        public Task<LauncherConfiguration?> ReadLauncherConfiguration(string launcherConfigurationFile, Func<string, LauncherConfiguration?> deserialize) => ReadLauncherConfiguration(launcherConfigurationFile, (content) => Task.FromResult(deserialize(content)));
    }
}
