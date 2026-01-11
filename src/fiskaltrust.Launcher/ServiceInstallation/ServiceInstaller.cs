using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public abstract class ServiceInstaller
    {
        protected readonly LauncherExecutablePath _launcherExecutablePath;
        protected readonly ILogger _logger;

        protected ServiceInstaller(LauncherExecutablePath launcherExecutablePath, ILogger logger)
        {
            _launcherExecutablePath = launcherExecutablePath;
            _logger = logger;
        }

        public abstract Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false);

        public abstract Task<int> UninstallService();
    }
}
