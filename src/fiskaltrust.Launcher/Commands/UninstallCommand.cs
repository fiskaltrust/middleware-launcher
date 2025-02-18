using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using fiskaltrust.Launcher.ServiceInstallation;
using fiskaltrust.Launcher.Helpers;

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
            ServiceInstaller? installer = null;
            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(uninstallOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", 
                    uninstallServices.LauncherExecutablePath, commonProperties.LauncherConfiguration.ServiceFolder);
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(uninstallOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", uninstallServices.LauncherExecutablePath);
            }

            if (installer is not null)
            {
                return await installer.UninstallService().ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service uninstallation see: {link}", ""); // TODO
            return 1;
        }
    }
}
