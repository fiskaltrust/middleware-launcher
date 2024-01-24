using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Hosting.Systemd;
using Serilog;
using System.CommandLine;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class LinuxSystemD : ServiceInstaller
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";

        public LinuxSystemD(string? serviceName, LauncherExecutablePath launcherExecutablePath) : base(launcherExecutablePath)
        {
            _serviceName = serviceName ?? _serviceName;
        }

        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (!await IsSystemd())
            {
                Log.Error("No SystemD on this machine. No service installation possible.");
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
            await ProcessHelper.RunProcess("systemctl", new[] { "daemon-reload" });
            Log.Information("Starting systemd service.");
            await ProcessHelper.RunProcess("systemctl", new[] { "start", _serviceName });
            Log.Information("Enabling systemd service.");
            return (await ProcessHelper.RunProcess("systemctl", new[] { "enable", _serviceName, "-q" })).exitCode;
        }

        public override async Task<int> UninstallService()
        {
            if (!await IsSystemd())
            {
                Log.Error("No SystemD on this machine. No service uninstallation possible.");
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

            var command = $"sudo {processPath} {commandArgs} isSystemd";
            return new[]
            {
                "[Unit]",
                $"Description=\"{serviceDescription}\"",
                "",
                "[Service]",
                "Type=notify",
                $"ExecStart={command}",
                "TimeoutSec=0",
                $"WorkingDirectory={Path.GetDirectoryName(_launcherExecutablePath.Path)}",
                "",
                "[Install]",
                "WantedBy = multi-user.target"
            };
        }

        private static async Task<bool> IsSystemd()
        {
            var (exitCode, output) = await ProcessHelper.RunProcess("ps", new[] { "--no-headers", "-o", "comm", "1" });

            if (exitCode != 0 && output.Contains("systemd"))
            {
                Log.Error("Service installation works only for systemd setup.");
                return false;
            }
            return true;
        }

        private static async Task<bool> IsSystemdServiceInstalled(string serviceName)
        {
            var (exitCode, _) = await ProcessHelper.RunProcess("systemctl", new[] { $"status {serviceName}" });
            if (exitCode == 4)
            {
                return false;
            }
            return true;
        }
    }
}
