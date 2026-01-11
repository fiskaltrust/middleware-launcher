using System.Diagnostics;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Download;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace fiskaltrust.Launcher.Helpers
{
    public record LauncherProcessId(int Id);
    public record LauncherExecutablePath
    {
        private string _path = null!;
        public string Path { get => System.IO.Path.GetFullPath(_path); init => _path = value; }

        public override string ToString() => Path;
    };

    public class SelfUpdater
    {
        private readonly LauncherProcessId _processId;
        private readonly LauncherExecutablePath _executablePath;
        private bool _updatePending = false;

        public SelfUpdater(LauncherProcessId processId, LauncherExecutablePath executablePath)
        {
            _processId = processId;
            _executablePath = executablePath;
        }

        public async Task PrepareSelfUpdate(ILogger logger, LauncherConfiguration launcherConfiguration, PackageDownloader packageDownloader)
        {
#if EnableSelfUpdate
            logger.SelfUpdateEnabled();
            var configuredVersion = launcherConfiguration.LauncherVersion;
#else
            logger.SelfUpdateDisabled();
            var configuredVersion = new SemanticVersioning.Range("*");
#endif
            if (configuredVersion is not null && Common.Constants.Version.CurrentVersion is not null)
            {
                SemanticVersioning.Version? launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, configuredVersion, Constants.Runtime.Identifier, Common.Constants.Version.CurrentVersion.IsPreRelease);

                if (launcherVersion is not null && Common.Constants.Version.CurrentVersion != launcherVersion)
                {
#if EnableSelfUpdate
                    if (configuredVersion.ToString() == launcherVersion.ToString())
                    {
                        if (Common.Constants.Version.CurrentVersion < launcherVersion)
                        {
                            logger.NewLauncherVersionSet();
                        }
                        else
                        {
                            logger.OlderLauncherVersionSet();
                        }
                    }
                    else
                    {
                        if (Common.Constants.Version.CurrentVersion < launcherVersion)
                        {
                            logger.NewLauncherVersionFoundForRange(configuredVersion.ToString());
                        }
                        else
                        {
                            logger.OlderLauncherVersionFoundForRange(configuredVersion.ToString());
                        }
                    }
#else
                    logger.NewLauncherVersionAvailable(launcherVersion.ToString());
#endif


#if EnableSelfUpdate
                    logger.DownloadingNewVersion(launcherVersion.ToString());

                    try
                    {
                        await packageDownloader.DownloadLauncherAsync(launcherVersion);
                        if (Common.Constants.Version.CurrentVersion < launcherVersion)
                        {
                            logger.LauncherWillBeUpdatedOnShutdown(launcherVersion.ToString());
                        }
                        else
                        {
                            logger.LauncherWillBeDowngradedOnShutdown(launcherVersion.ToString());
                        }

                        _updatePending = true;
                    }
                    catch (Exception e)
                    {
                        logger.CouldNotDownloadNewLauncherVersion(e);
                    }
#endif
                }
            }
        }


        public async Task StartSelfUpdate(ILogger logger, LauncherConfiguration launcherConfiguration, string launcherConfigurationFile)
        {
#if EnableSelfUpdate
            if (_updatePending)
            {
                var newExecutablePath = Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId?.ToString()!, "fiskaltrust.Launcher");
                var process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = Path.Combine(newExecutablePath, $"fiskaltrust.LauncherUpdater{(OperatingSystem.IsWindows() ? ".exe" : "")}");
                process.StartInfo.CreateNoWindow = false;

                process.StartInfo.Arguments = string.Join(" ", new string[] {
                "--launcher-process-id", _processId.Id.ToString(),
                "--from", $"\"{Path.Combine(newExecutablePath, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}")}\"",
                "--to", $"\"{_executablePath.Path}\"",
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(launcherConfiguration.Serialize()))}\"",
                "--launcher-configuration-file", $"\"{Path.GetFullPath(launcherConfigurationFile)}\"",
            });

                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;

                process.Start();

                logger.LauncherUpdateStartingInBackground();

                await Task.Delay(TimeSpan.FromSeconds(3));

                if (process.HasExited)
                {
                    logger.LauncherUpdateFailed(launcherConfiguration.LogFolder!);
                    var withEnrichedContext = (Action log) =>
                    {
                        using var enrichedContext = LogContext.PushProperty("EnrichedContext", " LauncherUpdater");
                        log();
                    };

                    var stdOut = await process.StandardOutput.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(stdOut))
                    {
                        withEnrichedContext(() => logger.LogInformation("{StdOut}", stdOut));
                    }
                    var stdErr = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(stdErr))
                    {
                        withEnrichedContext(() => logger.LogError("{StdErr}", stdErr));
                    }
                }
            }
#endif
        }
    }
}
