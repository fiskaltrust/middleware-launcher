using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace fiskaltrust.Launcher.Helpers;

public static class ProcessHelper
{
    public static async Task<(int exitCode, string output)> RunProcess(
        string fileName, 
        IEnumerable<string> arguments, 
        LogEventLevel logLevel = LogEventLevel.Information)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(" ", arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        if (!string.IsNullOrEmpty(stdOut))
        {
            Log.Write(logLevel, stdOut);
        }

        var stdErr = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrEmpty(stdErr))
        {
            Log.Write(LogEventLevel.Warning, stdErr);
        }

        if (process.ExitCode != 0)
        {
            Log.Warning($"Process {fileName} exited with code {process.ExitCode}");
        }

        return (process.ExitCode, stdOut);
    }
}