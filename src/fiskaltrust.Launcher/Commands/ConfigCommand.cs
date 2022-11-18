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

        public Guid? CipyerCashboxId { get; set; }
        public string? CipyerAccessToken { get; set; }

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
                    try
                    {
                        try
                        {
                            launcherConfiguration.Decrypt(CipyerAccessToken);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, "Error decrypring launcher configuration file.");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error decrypring launcher configuration file.");
                    }
                    rawLauncherConfigurationOld = launcherConfiguration.Serialize(true, true);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not read launcher configuration");
                    return 1;
                }
            }

            launcherConfiguration.OverwriteWith(ArgsLauncherConfiguration);
            launcherConfiguration.DisableDefaults();

            string rawLauncherConfigurationNew;
            try
            {
                rawLauncherConfigurationNew = launcherConfiguration.Serialize(true, true);
                try
                {
                    launcherConfiguration.Encrypt(CipyerCashboxId, CipyerAccessToken);
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Error encrypring launcher configuration file.");
                }
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

        public Guid? CipyerCashboxId { get; set; }
        public string? CipyerAccessToken { get; set; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            Log.Logger = new LoggerConfiguration()
                .AddLoggingConfiguration()
                .CreateLogger();

            LauncherConfiguration? localConfiguration = null;
            if (LauncherConfigurationFile is not null)
            {
                try
                {
                    localConfiguration = LauncherConfiguration.Deserialize(await File.ReadAllTextAsync(LauncherConfigurationFile));
                    try
                    {
                        localConfiguration.Decrypt(CipyerAccessToken);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error decrypring launcher configuration file.");
                    }
                    localConfiguration.DisableDefaults();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not read launcher configuration.");
                }

                if (localConfiguration is not null)
                {
                    Log.Information("Local configuration {LauncherConfigurationFile}\n{localConfiguration}", LauncherConfigurationFile, localConfiguration.Serialize(true, true));
                }
            }

            if (LegacyConfigFile is not null)
            {
                LauncherConfiguration? legacyConfiguration = null;
                try
                {
                    legacyConfiguration = await LegacyConfigFileReader.ReadLegacyConfigFile(LegacyConfigFile);
                    legacyConfiguration?.Decrypt(CipyerAccessToken);
                    legacyConfiguration?.DisableDefaults();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not read legacy launcher configuration.");
                }

                if (legacyConfiguration is not null)
                {
                    Log.Information("Legacy configuration {LegacyConfigFile}\n{legacyConfiguration}", LegacyConfigFile, legacyConfiguration.Serialize(true, true));
                }
            }

            CashBoxConfigurationFile ??= localConfiguration?.CashboxConfigurationFile;
            if (CashBoxConfigurationFile is not null)
            {
                LauncherConfiguration? remoteConfiguration = null;
                try
                {
                    remoteConfiguration = LauncherConfigurationInCashBoxConfiguration.Deserialize(await File.ReadAllTextAsync(CashBoxConfigurationFile));
                    try
                    {
                        remoteConfiguration?.Decrypt(CipyerAccessToken);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "Error decrypring launcher configuration file.");
                    }
                    remoteConfiguration?.DisableDefaults();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Could not read remote launcher configuration.");
                }

                if (remoteConfiguration is not null)
                {
                    Log.Information("Remote configuration from {CashBoxConfigurationFile}\n{remoteConfiguration}", CashBoxConfigurationFile, remoteConfiguration.Serialize(true, true));
                }
            }

            return 0;
        }
    }
}
