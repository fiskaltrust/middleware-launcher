using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class LinuxSystemD : ServiceInstaller
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";
        private readonly string? _serviceFolder;

        public LinuxSystemD(string? serviceName, LauncherExecutablePath launcherExecutablePath, string? serviceFolder, ILogger logger) : base(launcherExecutablePath, logger)
        {
            _serviceName = serviceName ?? _serviceName;
            _serviceFolder = serviceFolder;
        }

        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (!await IsSystemdAvailable())
            {
                _logger.SystemdNotRunning();
                return -1;
            }

            if (await IsSystemdServiceInstalled(_serviceName))
            {
                _logger.ServiceAlreadyInstalled();
                return -1;
            }
            _logger.InstallingServiceViaSystemd();
            var serviceFileContent = GetServiceFileContent(displayName ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
            await ProcessHelper.RunProcess("systemctl", ["daemon-reload"]);
            _logger.StartingSystemdService();
            await ProcessHelper.RunProcess("systemctl", ["start", _serviceName]);
            _logger.EnablingSystemdService();
            return (await ProcessHelper.RunProcess("systemctl", ["enable", _serviceName, "-q"])).exitCode;
        }

        public override async Task<int> UninstallService()
        {
            if (!await IsSystemdAvailable())
            {
                _logger.SystemdNotRunningUninstall();
                return -1;
            }

            if (!await IsSystemdServiceInstalled(_serviceName))
            {
                _logger.ServiceNotInstalled();
                return -1;
            }

            _logger.StoppingSystemdService();
            await ProcessHelper.RunProcess("systemctl", ["stop ", _serviceName]);
            _logger.DisablingSystemdService();
            await ProcessHelper.RunProcess("systemctl", ["disable ", _serviceName, "-q"]);
            _logger.RemovingSystemdService();
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await ProcessHelper.RunProcess("rm", [serviceFilePath]);
            _logger.ReloadingSystemdDaemon();
            await ProcessHelper.RunProcess("systemctl", ["daemon-reload"]);
            _logger.ResettingSystemdFailedUnits();
            return (await ProcessHelper.RunProcess("systemctl", ["reset-failed"])).exitCode;
        }

        private string[] GetServiceFileContent(string serviceDescription, string commandArgs)
        {
            var processPath = _launcherExecutablePath.Path;
            var workingDirectory = Path.GetDirectoryName(_launcherExecutablePath.Path);
            var command = $"{processPath} {commandArgs}";

            return [
                "[Unit]",
                $"Description=\"{serviceDescription}\"",
                $"RequiresMountsFor={_serviceFolder} {workingDirectory}",
                $"Wants=network-online.target",
                $"After=network.target,network-online.target",
                "",
                "[Service]",
                "Type=notify",
                $"ExecStart={command}",
                $"WorkingDirectory={workingDirectory}",
                "",
                "[Install]",
                "WantedBy = multi-user.target"
            ];
        }

        private async Task<bool> IsSystemdAvailable()
        {
            var (exitCode, output) = await ProcessHelper.RunProcess("ps", ["--no-headers", "-o", "comm", "1"], logLevel: null);

            if (exitCode != 0 && output.Contains("systemd"))
            {
                _logger.ServiceInstallationOnlyForSystemd();
                return false;
            }
            return true;
        }

        private static async Task<bool> IsSystemdServiceInstalled(string serviceName)
        {
            var (exitCode, _) = await ProcessHelper.RunProcess("systemctl", [$"status {serviceName}"], logLevel: null);
            if (exitCode == 4)
            {
                return false;
            }
            return true;
        }
    }
}
