using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Download;
using fiskaltrust.storage.serialization.V0;
using Microsoft.Extensions.Hosting.WindowsServices;

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

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Started all packages.");
                if (!WindowsServiceHelpers.IsWindowsService())
                {
                    _logger.LogInformation("Press CTRL+C to exit.");
                }
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
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

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

            if (!cancellationToken.IsCancellationRequested)
            {
                _hosts.Add(
                    configuration.Id,
                    monarch
                );
            }

            try
            {
                await monarch.Start(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Started {Package} {Id}.", configuration.Package, configuration.Id);
                }
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
            _logger.LogInformation("fiskaltrust.Launcher: {version}", Common.Constants.Version.CurrentVersion);
            _logger.LogInformation("OS:                   {OS}, {Bit}", Environment.OSVersion.VersionString, Environment.Is64BitOperatingSystem ? "64Bit" : "32Bit");
            if (OperatingSystem.IsWindows())
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                _logger.LogInformation("Admin User:           {admin}", principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
            }
            _logger.LogInformation("CWD:                  {CWD}", Path.GetFullPath("./"));
            _logger.LogInformation("CashBoxId:            {CashBoxId}", _launcherConfiguration.CashboxId);
        }
    }

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;
        private readonly PackageConfiguration _packageConfiguration;
        private readonly ILogger<ProcessHostMonarch> _logger;

        public ProcessHostMonarch(ILogger<ProcessHostMonarch> logger, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PackageType packageType)
        {
            _packageConfiguration = packageConfiguration;
            _logger = logger;

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");
            _process.StartInfo.CreateNoWindow = false;

            _process.StartInfo.Arguments = string.Join(" ", new string[] {
                "host",
                "--plebian-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Serializer.Serialize(new PlebianConfiguration { PackageType = packageType, PackageId = packageConfiguration.Id }, Helpers.Serialization.SerializerContext.Default)))}\"",
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Serializer.Serialize(launcherConfiguration, SerializerContext.Default)))}\"",
            });

            // if(Debugger.IsAttached)
            // {
            //     _process.StartInfo.Arguments += " --debugging";
            // }
            _process.StartInfo.RedirectStandardInput = true;
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
                    if (!_process.HasExited)
                    {
                        _logger.LogInformation("Killing {package} {id}.", _packageConfiguration.Package, _packageConfiguration.Id);
                        _process.Kill();
                    }
                }
                catch { }
            });

            _process.Exited += async (_, __) =>
            {
                _logger.LogInformation("Host {package} {id} has shutdown.", _packageConfiguration.Package, _packageConfiguration.Id);

                await Task.Delay(1000);
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (_process.ExitCode != 0)
                    {
                        try
                        {
                            _logger.LogInformation("Restarting {package} {id}.", _packageConfiguration.Package, _packageConfiguration.Id);
                            if (!_process.Start()) { throw new Exception($"Process.Start() was false for {_packageConfiguration.Package} {_packageConfiguration.Id}"); }

                            try
                            {
                                _process.BeginOutputReadLine();
                                _process.BeginErrorReadLine();
                            }
                            catch { }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Could not start ProcessHost process for {package} {id}.", _packageConfiguration.Package, _packageConfiguration.Id);
                            _started.TrySetResult();
                            _stopped.TrySetCanceled(cancellationToken);
                        }
                    }
                    else
                    {
                        _started.TrySetCanceled();
                        _stopped.SetResult();
                    }
                }
                else
                {
                    _started.TrySetResult();
                    _stopped.SetResult();
                }
            };

            try
            {
                if (!_process.Start()) { throw new Exception("Process.Start() was false"); }
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
