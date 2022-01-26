using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using System.Diagnostics;
using Serilog.Context;

namespace fiskaltrust.Launcher.Commands
{
    public class UninstallCommand : CommonCommand
    {
        public UninstallCommand() : base("uninstall")
        {
            AddOption(new Option<string?>("--service-name"));
        }
    }

    public class UninstallCommandHandler : CommonCommandHandler
    {
        public string? ServiceName { get; set; }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }
            if (OperatingSystem.IsLinux())
            {
                var installLinux = new InstallLinuxSystemd(LauncherConfigurationFile, ServiceName, "");
                return installLinux.UninstallSystemd();
            }
            if (!OperatingSystem.IsWindows())
            {
                Log.Error("For non windows or linux(systemd) service installation see: {link}", ""); // TODO
                return 1;
            }

            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    Log.Error("Run as admin to uninstall service {link}", ""); // TODO
                    return 1;
                }
            }

            var serviceName = ServiceName ?? $"fiskaltrust-{_launcherConfiguration.CashboxId}";

            Log.Information("Stopping service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "stop", $"\"{serviceName}\"" }))
            {
                Log.Warning($"Could not stop service \"{serviceName}\"");
            }

            Log.Information("Uninstalling service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "delete", $"\"{serviceName}\"" }))
            {
                Log.Warning($"Could not uninstall service \"{serviceName}\"");
                return 1;
            }
            Log.Information($"successfully uninstalled service \"{serviceName}\"");

            return 0;
        }

        private static async Task<bool> RunProcess(string fileName,  IEnumerable<string> arguments)
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

            var withEnrichedContext = (Action log) => {
                var enrichedContext = LogContext.PushProperty("EnrichedContext", " sc.exe");
                log();
                enrichedContext.Dispose();
            };

            process.OutputDataReceived += (data, e) => withEnrichedContext(() => Log.Information(e.Data));
            process.ErrorDataReceived += (data, e) => withEnrichedContext(() => Log.Error(e.Data));

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
    }
}
