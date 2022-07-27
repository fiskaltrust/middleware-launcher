using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class WindowsService : ServiceInstaller
    {
        private readonly string _serviceName;

        public WindowsService(string serviceName)
        {
            _serviceName = serviceName;
        }
        public override async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false)
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    Log.Error("Run as admin to install service {link}", ""); // TODO
                    return 1;
                }
            }
            else
            {
                Log.Error("Wrong Operating system.");
                return 1;
            }

            var processPath = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");

            var arguments = new List<string> {
                "create",
                $"\"{_serviceName}\"",
                $"start={(delayedStart ? "delayed-auto" : "auto")}",
                $"binPath=\"{processPath} {commandArgs.Replace("\"", "\\\"")}\"",
                // $"depend=" // TODO
            };

            if (displayName is not null)
            {
                arguments.Add($"DisplayName=\"{displayName}\"");
            }

            Log.Information("Installing service.");
            if ((await RunProcess(@"C:\WINDOWS\system32\sc.exe", arguments)).exitCode != 0)
            {
                Log.Information($"Could not install service \"{_serviceName}\".");
                return 1;
            }

            Log.Information("Starting service.");
            if ((await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "start", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                Log.Warning($"Could not start service \"{_serviceName}\".");
            }
            else
            {
                Log.Information($"successfully installed service \"{_serviceName}\".");
            }

            return 0;
        }
        public override async Task<int> UninstallService()
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    Log.Error("Run as admin to uninstall service {link}", ""); // TODO
                    return 1;
                }
            }
            else
            {
                Log.Error("Wrong Operating system.");
                return 1;
            }

            Log.Information("Stopping service.");
            if ((await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "stop", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                Log.Warning($"Could not stop service \"{_serviceName}\".");
            }

            Log.Information("Uninstalling service.");
            if ((await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "delete", $"\"{_serviceName}\"" })).exitCode != 0)
            {
                Log.Warning($"Could not uninstall service \"{_serviceName}\".");
                return 1;
            }
            Log.Information($"successfully uninstalled service \"{_serviceName}\".");
            return 0;
        }
    }
}
