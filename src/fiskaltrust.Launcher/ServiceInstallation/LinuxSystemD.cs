using fiskaltrust.Launcher.Helpers;
using Serilog;

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
                return -1;
            }
            commandArgs += " --is-systemd-service true";
            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileContent(displayName ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
            await ProcessHelper.RunProcess("systemctl", new[] { "daemon-reload" });
            Log.Information("Starting service.");
            await ProcessHelper.RunProcess("systemctl", new[] { "start", _serviceName });
            Log.Information("Enable service.");
            return (await ProcessHelper.RunProcess("systemctl", new[] { "enable", _serviceName, "-q" })).exitCode;
        }

        public override async Task<int> UninstallService()
        {
            if (!await IsSystemd())
            {
                return -1;
            }
            Log.Information("Stop service on systemd.");
            await ProcessHelper.RunProcess("systemctl", new[] { "stop ", _serviceName });
            Log.Information("Disable service.");
            await ProcessHelper.RunProcess("systemctl", new[] { "disable ", _serviceName, "-q" });
            Log.Information("Remove service.");
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await ProcessHelper.RunProcess("rm", new[] { serviceFilePath });
            Log.Information("Reload daemon.");
            await ProcessHelper.RunProcess("systemctl", new[] { "daemon-reload" });
            Log.Information("Reset failed.");
            return (await ProcessHelper.RunProcess("systemctl", new[] { "reset-failed" })).exitCode;
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

        private string[] GetServiceFileContent(string serviceDescription, string commandArgs)
        {
            var processPath = _launcherExecutablePath.Path;

            var command = $"sudo {processPath} {commandArgs}";
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
    }
}
