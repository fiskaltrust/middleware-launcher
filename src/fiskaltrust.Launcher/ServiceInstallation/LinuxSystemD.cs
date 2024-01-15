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

            if(await IsSystemdServiceInstalled(_serviceName))
            {
                Log.Error("Service is already installed and cannot be installed twice to one cashbox.");
                return -1;
            }
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
                Log.Error("No SystemD on this machine. No service uninstallation possible.");
                return -1;
            }

            if (!await IsSystemdServiceInstalled(_serviceName))
            {
                Log.Error("Service is not installed!");
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
            var (exitCode, output) = await ProcessHelper.RunProcess("systemctl", new[] { $"status {serviceName}" });
            Log.Information($"exitCode: {exitCode}, output: {output}");
            if (exitCode != 0 && !output.Contains("serviceName"))
            {
                return false;
            }
            return true;
        }   
    }
}
