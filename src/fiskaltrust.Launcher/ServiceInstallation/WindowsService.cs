using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class WindowsService : ServiceInstaller
    {
        private readonly string _serviceName;

        public WindowsService(string serviceName, LauncherExecutablePath launcherExecutablePath, ILogger logger) : base(launcherExecutablePath, logger)
        {
            _serviceName = serviceName;
        }
        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (OperatingSystem.IsWindows())
            {

                if (!Runtime.IsAdministrator!.Value)
                {
                    _logger.RunAsAdminToInstallService(""); // TODO
                    return 1;
                }
            }
            else
            {
                _logger.WrongOperatingSystem();
                return 1;
            }

            var processPath = _launcherExecutablePath.Path;

            var arguments = new List<string> {
                "create",
                $"\"{_serviceName}\"",
                "start=", $"{(delayedStart ? "delayed-auto" : "auto")}",
                "binPath=", $"\"{processPath} {commandArgs.Replace("\"", "\\\"")}\"",
                // $"depend=" // TODO
            };

            if (displayName is not null)
            {
                arguments.AddRange(new string[] { "DisplayName=", $"\"{displayName}\"" });
            }

            _logger.InstallingService();
            if ((await ProcessHelper.RunProcess(@"C:\WINDOWS\system32\sc.exe", arguments)).exitCode != 0)
            {
                _logger.CouldNotInstallService(_serviceName);
                return 1;
            }

            _logger.StartingService();
            if ((await ProcessHelper.RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "start", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                _logger.CouldNotStartService(_serviceName);
            }
            else
            {
                _logger.SuccessfullyInstalledService(_serviceName);
            }

            return 0;
        }
        public override async Task<int> UninstallService()
        {
            if (OperatingSystem.IsWindows())
            {
                if (!Runtime.IsAdministrator!.Value)
                {
                    _logger.RunAsAdminToUninstallService(""); // TODO
                    return 1;
                }
            }
            else
            {
                _logger.WrongOperatingSystem();
                return 1;
            }

            _logger.StoppingService();
            if ((await ProcessHelper.RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "stop", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                _logger.CouldNotStopService(_serviceName);
            }

            _logger.UninstallingService();
            if ((await ProcessHelper.RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "delete", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                _logger.CouldNotUninstallService(_serviceName);
                return 1;
            }
            _logger.SuccessfullyUninstalledService(_serviceName);
            return 0;
        }
    }
}
