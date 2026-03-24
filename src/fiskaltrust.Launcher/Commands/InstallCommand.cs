using System.CommandLine;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.ServiceInstallation;
using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = fiskaltrust.Launcher.Common.Extensions.LoggerExtensions;

namespace fiskaltrust.Launcher.Commands
{
    public class InstallCommand : CommonCommand
    {
        public InstallCommand() : base("install")
        {
            AddOption(new Option<string?>("--service-name"));
            AddOption(new Option<string?>("--service-display-name"));
            AddOption(new Option<string?>("--service-description"));
            AddOption(new Option<bool>("--delayed-start"));
        }
    }

    public class InstallOptions
    {
        public InstallOptions(string? ServiceName, string? ServiceDisplayName, string? ServiceDescription, bool DelayedStart)
        {
            this.ServiceName = ServiceName;
            this.ServiceDisplayName = ServiceDisplayName;
            this.ServiceDescription = ServiceDescription;
            this.DelayedStart = DelayedStart;
        }

        public string? ServiceName { get; set; }
        public string? ServiceDisplayName { get; set; }
        public string? ServiceDescription { get; set; }
        public bool DelayedStart { get; set; }
    }

    public class InstallServices
    {
        public InstallServices(SubArguments subArguments, LauncherExecutablePath launcherExecutablePath)
        {
            SubArguments = subArguments;
            LauncherExecutablePath = launcherExecutablePath;
        }

        public readonly SubArguments SubArguments;
        public readonly LauncherExecutablePath LauncherExecutablePath;

    }

    public static class InstallHandler
    {
        public static async Task<int> HandleAsync(CommonOptions commonOptions, CommonProperties commonProperties, InstallOptions installOptions, InstallServices installServices)
        {
            var logger = Serilog.Log.Logger.ToDotnetLogger();

            var commandArgs = "run ";
            commandArgs += string.Join(" ", new[] {
                "--cashbox-id", commonProperties.LauncherConfiguration.CashboxId!.Value.ToString(),
                "--access-token", commonProperties.LauncherConfiguration.AccessToken!,
                "--sandbox", commonProperties.LauncherConfiguration.Sandbox!.Value.ToString(),
                "--launcher-configuration-file", $"\"{commonOptions.LauncherConfigurationFile}\"",
                "--legacy-configuration-file", $"\"{commonOptions.LegacyConfigurationFile}\"",
            }.Concat(installServices.SubArguments.Args));

            ServiceInstaller? installer = null;

            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(installOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}",
                    installServices.LauncherExecutablePath, commonProperties.LauncherConfiguration.ServiceFolder, logger);
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(installOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", installServices.LauncherExecutablePath, logger);
            }

            if (installer is not null)
            {
                return await installer.InstallService(commandArgs, installOptions.ServiceDisplayName, installOptions.DelayedStart).ConfigureAwait(false);
            }

            logger.ServiceInstallationManualRequired();
            return 1;
        }
    }
}
