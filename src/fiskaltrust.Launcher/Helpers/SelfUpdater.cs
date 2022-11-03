
using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using Serilog;
using Serilog.Context;

namespace fiskaltrust.Launcher.Helpers
{
    public record LauncherProcessId(int Id);
    public record LauncherExecutablePath(string Path);

    public class SelfUpdater
    {
        private readonly LauncherProcessId _processId;
        private readonly LauncherExecutablePath _executablePath;

        public SelfUpdater(LauncherProcessId processId, LauncherExecutablePath executablePath)
        {
            _processId = processId;
            _executablePath = executablePath;
        }

        public async Task StartSelfUpdate(Serilog.ILogger logger, LauncherConfiguration launcherConfiguration)
        {
            var newExecutablePath = Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId?.ToString()!, "fiskaltrust.Launcher");
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = Path.Combine(newExecutablePath, $"fiskaltrust.LauncherUpdater{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", new string[] {
                "--launcher-process-id", _processId.Id.ToString(),
                "--from", $"\"{Path.Combine(newExecutablePath, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}")}\"",
                "--to", _executablePath.Path,
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(launcherConfiguration.Serialize()))}\"",
            });

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;

            process.Start();

            logger.Information("Launcher update starting in the background.");

            Thread.Sleep(TimeSpan.FromSeconds(10));

            if (process.HasExited)
            {
                logger.Error("Launcher Update failed. See {LogFolder} for the update log.", launcherConfiguration.LogFolder);
                var withEnrichedContext = (Action log) =>
                {
                    using var enrichedContext = LogContext.PushProperty("EnrichedContext", " LauncherUpdater");
                    log();
                };

                var stdOut = await process.StandardOutput.ReadToEndAsync();
                if (!string.IsNullOrEmpty(stdOut))
                {
                    withEnrichedContext(() => logger.Information(stdOut));
                }
                var stdErr = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrEmpty(stdErr))
                {
                    withEnrichedContext(() => logger.Error(stdErr));
                }
            }
        }
    }
}