using fiskaltrust.Launcher.Helpers;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServiceInstallation
{
    public abstract class ServiceInstaller
    {
        protected readonly LauncherExecutablePath _launcherExecutablePath;

        protected ServiceInstaller(LauncherExecutablePath launcherExecutablePath)
        {
            _launcherExecutablePath = launcherExecutablePath;
        }

        public abstract Task<int> InstallService(string commandArgs, string? displayName, bool delayedStart = false);

        public abstract Task<int> UninstallService();

        protected static async Task<(int exitCode, string output)> RunProcess(string fileName, IEnumerable<string> arguments)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = fileName;
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", arguments);
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            await process.WaitForExitAsync();

            var withEnrichedContext = (Action log) =>
            {
                var enrichedContext = LogContext.PushProperty("EnrichedContext", $" {Path.GetFileName(fileName)}");
                log();
                enrichedContext.Dispose();
            };

            var stdOut = await process.StandardOutput.ReadToEndAsync();
            if (!string.IsNullOrEmpty(stdOut))
            {
                withEnrichedContext(() => Log.Information(stdOut));
            }

            var stdErr = await process.StandardError.ReadToEndAsync();
            if (!string.IsNullOrEmpty(stdErr))
            {
                withEnrichedContext(() => Log.Error(stdErr));
            }

            return (process.ExitCode, stdOut);
        }
    }
}
