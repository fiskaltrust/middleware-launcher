﻿using Serilog;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class LinuxSystemD : ServiceInstaller
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";

        public LinuxSystemD(string? serviceName)
        {
            _serviceName = serviceName ?? _serviceName;
        }

        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (!await IsSystemd())
            {
                return -1;
            }
            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileContent(displayName ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, $"{_serviceName}.service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
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

        private static string[] GetServiceFileContent(string serviceDescription, string commandArgs)
        {
            var processPath = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");

            var command = $"{processPath} {commandArgs}";
            return new[]
            {
                "[Unit]",
                $"Description={serviceDescription}",
                "",
                "[Service]",
                "Type=simple",
                $"ExecStart={command}",
                "",
                "[Install]",
                "WantedBy = multi-user.target"
            };
        }
    }
}
