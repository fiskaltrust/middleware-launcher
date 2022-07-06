using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public class WindowsService
    {
        private readonly string _serviceName;

        public WindowsService(string serviceName)
        {
            _serviceName = serviceName;
        }
        public async Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart)
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
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", arguments))
            {
                Log.Information($"Could not install service \"{_serviceName}\".");
                return 1;
            }

            Log.Information("Starting service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "start", $"\"{_serviceName}\"" }))
            {
                Log.Warning($"Could not start service \"{_serviceName}\".");
            }
            else
            {
                Log.Information($"successfully installed service \"{_serviceName}\".");
            }

            return 0;
        }
        public async Task<int> UninstallService()
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
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "stop", $"\"{_serviceName}\"" }))
            {
                Log.Warning($"Could not stop service \"{_serviceName}\".");
            }

            Log.Information("Uninstalling service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "delete", $"\"{_serviceName}\"" }))
            {
                Log.Warning($"Could not uninstall service \"{_serviceName}\".");
                return 1;
            }
            Log.Information($"successfully uninstalled service \"{_serviceName}\".");
            return 0;
        }

        private static async Task<bool> RunProcess(string fileName, IEnumerable<string> arguments)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = fileName;
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", arguments);
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var withEnrichedContext = (Action log) =>
            {
                var enrichedContext = LogContext.PushProperty("EnrichedContext", " sc.exe");
                log();
                enrichedContext.Dispose();
            };

            process.OutputDataReceived += (_, e) => withEnrichedContext(() => Log.Information(e.Data));
            process.ErrorDataReceived += (_, e) => withEnrichedContext(() => Log.Error(e.Data));

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
    }
}
