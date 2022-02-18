using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Download;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{
    public class ProcessHostMonarcStartup : BackgroundService
    {
        public class AlreadyLoggedException : Exception { }

        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ftCashBoxConfiguration _cashBoxConfiguration;
        private readonly PackageDownloader _downloader;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHostApplicationLifetime _lifetime;

        public ProcessHostMonarcStartup(ILoggerFactory loggerFactory, ILogger<ProcessHostMonarcStartup> logger, Dictionary<Guid, ProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration, PackageDownloader downloader, IHostApplicationLifetime lifetime)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
            _downloader = downloader;
            _lifetime = lifetime;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            StartupLogging();

            try
            {
                foreach (var scu in _cashBoxConfiguration.ftSignaturCreationDevices)
                {
                    await StartProcessHostMonarch(scu, PackageType.SCU, cancellationToken);
                }

                foreach (var queue in _cashBoxConfiguration.ftQueues)
                {
                    await StartProcessHostMonarch(queue, PackageType.Queue, cancellationToken);
                }

                foreach (var helper in _cashBoxConfiguration.helpers)
                {
                    await StartProcessHostMonarch(helper, PackageType.Helper, cancellationToken);
                }
            }
            catch (Exception e)
            {
                if (e is not AlreadyLoggedException) { _logger.LogError(e, "Error Starting Monarchs"); }
                _lifetime.StopApplication();
                return;
            }

            try
            {
                await Task.WhenAll(_hosts.Select(h => h.Value.Stopped()));
            }
            catch
            {
                foreach (var failed in _hosts.Where(h => !h.Value.Stopped().IsCompletedSuccessfully).Select(h => (h.Key, h.Value.Stopped().Exception)))
                {
                    _logger.LogWarning(failed.Exception, "ProcessHost {Id} had crashed.", failed.Key);
                }
            }
        }

        private async Task StartProcessHostMonarch(PackageConfiguration configuration, PackageType packageType, CancellationToken cancellationToken)
        {
            try
            {
                await _downloader.DownloadPackageAsync(configuration);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not download package.");
                throw new AlreadyLoggedException();
            }

            var monarch = new ProcessHostMonarch(
                _loggerFactory.CreateLogger<ProcessHostMonarch>(),
                _launcherConfiguration,
                configuration,
                packageType);

            _hosts.Add(
                configuration.Id,
                monarch
            );

            try
            {
                await monarch.Start(cancellationToken);
                _logger.LogInformation("Started {Package} {Id}.", configuration.Package, configuration.Id);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Could not start {Package} {Id}.", configuration.Package, configuration.Id);
                // not throwing here keeps the launcher alive even when theres a package completely failed
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not start {Package} {Id}.", configuration.Package, configuration.Id);
                throw new AlreadyLoggedException();
            }
        }

        private void StartupLogging()
        {
            _logger.LogInformation("OS:         {OS}, {Bit}", Environment.OSVersion.VersionString, Environment.Is64BitOperatingSystem ? "64Bit" : "32Bit");
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                _logger.LogInformation("Admin User: {admin}", principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
            }
            _logger.LogInformation("CWD:        {CWD}", Path.GetFullPath("./"));
            _logger.LogInformation("CashBoxId:  {CashBoxId}", _launcherConfiguration.CashboxId);
        }
    }

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;
        private readonly ILogger<ProcessHostMonarch> _logger;

        public ProcessHostMonarch(ILogger<ProcessHostMonarch> logger, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PackageType packageType)
        {
            _logger = logger;

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");
            _process.StartInfo.CreateNoWindow = false;

            _process.StartInfo.Arguments = string.Join(" ", new string[] {
                "host",
                "--plebian-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new PlebianConfiguration { PackageType = packageType, PackageId = packageConfiguration.Id })))}\"",
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launcherConfiguration)))}\"",
            });

            // if(Debugger.IsAttached)
            // {
            //     _process.StartInfo.Arguments += " --debugging";
            // }

            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.RedirectStandardOutput = true;

            _process.EnableRaisingEvents = true;
            _stopped = new TaskCompletionSource();
            _started = new TaskCompletionSource();
        }

        public Task Start(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                try
                {
                    _process.Kill();
                }
                catch { }
            });

            _process.Exited += (sender, e) =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (_process.ExitCode != 0)
                    {
                        try
                        {
                            if (!_process.Start()) { throw new Exception("Could not start ProcessHost process."); }

                            _process.BeginOutputReadLine();
                            _process.BeginErrorReadLine();
                        }
                        catch
                        {
                            _stopped.SetCanceled(cancellationToken);
                            _started.TrySetResult();
                        }
                    }
                    else
                    {
                        _stopped.SetResult();
                        _started.TrySetCanceled();
                    }
                }
                else
                {
                    _stopped.SetResult();
                    _started.TrySetResult();
                }
            };

            try
            {
                if (!_process.Start()) { throw new Exception("Process.Start() was false."); }
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not start ProcessHost process.");
                _stopped.SetCanceled(cancellationToken);
                return Task.CompletedTask;
            }
            _logger.LogDebug("ProcessId {id}", _process.Id);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"ProcessId {_process.Id}");
            }

            return _started.Task;
        }

        public void Started()
        {
            _started.SetResult();
        }

        public Task Stopped()
        {
            return _stopped.Task;
        }
    }
}
