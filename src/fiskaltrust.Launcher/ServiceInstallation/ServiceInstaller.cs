using fiskaltrust.Launcher.Helpers;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public abstract class ServiceInstaller
    {
        protected readonly LauncherExecutablePath _launcherExecutablePath;

        protected ServiceInstaller(LauncherExecutablePath launcherExecutablePath)
        {
            _launcherExecutablePath = launcherExecutablePath;
        }

        public abstract Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false);

        public abstract Task<int> UninstallService();
        
        // The RunProcess method has been removed, as we now use ProcessHelper.RunProcess instead.
    }
}
