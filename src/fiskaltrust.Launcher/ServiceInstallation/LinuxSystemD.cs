﻿using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Helpers;
using Serilog;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class LinuxSystemD : ServiceInstaller
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";
        private readonly string _serviceUser;
        private readonly string _requiredDirectory;
        
        public LinuxSystemD(string? serviceName, LauncherExecutablePath launcherExecutablePath, LauncherConfiguration configuration)
            : base(launcherExecutablePath)
        {
            _serviceName = serviceName ?? "fiskaltrustLauncher";
            _serviceUser = Environment.GetEnvironmentVariable("USER");
            _requiredDirectory = configuration.ServiceFolder;
        }

        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (!await IsSystemd())
            {
                return -1;
            }

            // Creating a directory if does not exist
            if (!Directory.Exists(_requiredDirectory))
            {
                Directory.CreateDirectory(_requiredDirectory);

                // Change of directory owner
                await RunProcess("chown", new[] { _serviceUser, _requiredDirectory });

                // Changing directory permissions
                await RunProcess("chmod", new[] { "700", _requiredDirectory });
            }

            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileContent(displayName ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await File.WriteAllTextAsync(serviceFilePath, string.Join("\n", serviceFileContent)).ConfigureAwait(false);
            await RunProcess("systemctl", new[] { "daemon-reload" });
            Log.Information("Starting service.");
            await RunProcess("systemctl", new[] { "start", _serviceName });
            Log.Information("Enable service.");
            return (await RunProcess("systemctl", new[] { "enable", _serviceName, "-q" })).exitCode;
        }
        public override async Task<int> UninstallService()
        {
            if (!await IsSystemd())
            {
                return -1;
            }
            Log.Information("Stop service on systemd.");
            await RunProcess("systemctl", new[] { "stop ", _serviceName });
            Log.Information("Disable service.");
            await RunProcess("systemctl", new[] { "disable ", _serviceName, "-q" });
            Log.Information("Remove service.");
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await RunProcess("rm", new[] { serviceFilePath });
            Log.Information("Reload daemon.");
            await RunProcess("systemctl", new[] { "daemon-reload" });
            Log.Information("Reset failed.");
            return (await RunProcess("systemctl", new[] { "reset-failed" })).exitCode;
        }

        private static async Task<bool> IsSystemd()
        {
            var (exitCode, output) = await RunProcess("ps", new[] { "--no-headers", "-o", "comm", "1" });
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

            var command = $"{processPath} {commandArgs}";
            return new[]
            {
                "[Unit]",
                $"Description=\"{serviceDescription}\"",
                "",
                "[Service]",
                "Type=simple",
                $"ExecStart=\"{command}\"",
                "",
                "[Install]",
                "WantedBy = multi-user.target"
            };
        }
    }
}
