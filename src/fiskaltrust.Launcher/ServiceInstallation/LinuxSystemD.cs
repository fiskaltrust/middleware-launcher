using fiskaltrust.Launcher.Helpers;
using Serilog;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class LinuxSystemD : ServiceInstaller
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";
        private readonly string? _serviceFolder;

        public LinuxSystemD(string? serviceName, LauncherExecutablePath launcherExecutablePath, string? serviceFolder) : base(launcherExecutablePath)
        {
            _serviceName = serviceName ?? _serviceName;
            _serviceFolder = serviceFolder;
        }

        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (!await IdSystemdAvailable())
            {
                Log.Error("Systemd is not running on this machine. No service installation is possible.");
                return -1;
            }

            if (await IsSystemdServiceInstalled(_serviceName))
            {
                Log.Error("Service is already installed and cannot be installed twice for one cashbox.");
                return -1;
            }
            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileContent(displayName ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
            await ProcessHelper.RunProcess("systemctl", ["daemon-reload"]);
            Log.Information("Starting systemd service.");
            await ProcessHelper.RunProcess("systemctl", ["start", _serviceName]);
            Log.Information("Enabling systemd service.");
            return (await ProcessHelper.RunProcess("systemctl", ["enable", _serviceName, "-q"])).exitCode;
        }

        public override async Task<int> UninstallService()
        {
            if (!await IdSystemdAvailable())
            {
                Log.Error("Systemd is not running on this machine. No service uninstallation is possible.");
                return -1;
            }

            if (!await IsSystemdServiceInstalled(_serviceName))
            {
                Log.Error("Service is not installed!");
                return -1;
            }

            Log.Information("Stoppig systemd service.");
            await ProcessHelper.RunProcess("systemctl", ["stop ", _serviceName]);
            Log.Information("Disabling systemd service.");
            await ProcessHelper.RunProcess("systemctl", ["disable ", _serviceName, "-q"]);
            Log.Information("Removing systemd service.");
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await ProcessHelper.RunProcess("rm", [serviceFilePath]);
            Log.Information("Reloading systemd daemon.");
            await ProcessHelper.RunProcess("systemctl", ["daemon-reload"]);
            Log.Information("Reseting state for failed systemd units.");
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

        private static async Task<bool> IdSystemdAvailable()
        {
            var (exitCode, output) = await ProcessHelper.RunProcess("ps", ["--no-headers", "-o", "comm", "1"], logLevel: null);

            if (exitCode != 0 && output.Contains("systemd"))
            {
                Log.Error("Service installation works only for systemd setup.");
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
