using System.CommandLine;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.ServiceInstallation;
using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LoggerExtensions = fiskaltrust.Launcher.Common.Extensions.LoggerExtensions;

namespace fiskaltrust.Launcher.Commands
{
    public class UninstallCommand : CommonCommand
    {
        public UninstallCommand() : base("uninstall")
        {
            AddOption(new Option<string?>("--service-name"));
        }
    }

    public class UninstallOptions
    {
        public UninstallOptions(string? serviceName)
        {
            ServiceName = serviceName;
        }

        public string? ServiceName { get; set; }
    }

    public class UninstallServices
    {
        public UninstallServices(LauncherExecutablePath launcherExecutablePath)
        {
            LauncherExecutablePath = launcherExecutablePath;
        }

        public readonly LauncherExecutablePath LauncherExecutablePath;
    }

    public static class UninstallHandler
    {
        public static async Task<int> HandleAsync(CommonOptions _, CommonProperties commonProperties, UninstallOptions uninstallOptions, UninstallServices uninstallServices)
        {
            var logger = Serilog.Log.Logger.ToDotnetLogger();

            ServiceInstaller? installer = null;
            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(uninstallOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}",
                    uninstallServices.LauncherExecutablePath, commonProperties.LauncherConfiguration.ServiceFolder, logger);
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(uninstallOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", uninstallServices.LauncherExecutablePath, logger);
            }

            if (installer is not null)
            {
                return await installer.UninstallService().ConfigureAwait(false);
            }

            logger.ServiceUninstallationManualRequired();
            return 1;
        }
    }
}
