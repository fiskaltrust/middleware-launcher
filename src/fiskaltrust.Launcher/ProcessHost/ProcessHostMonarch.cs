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
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ftCashBoxConfiguration _cashBoxConfiguration;
        private readonly Downloader _downloader;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;

        public ProcessHostMonarcStartup(ILoggerFactory loggerFactory, ILogger<ProcessHostMonarcStartup> logger, Dictionary<Guid, ProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration, Downloader downloader)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
            _downloader = downloader;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            StartupLogging();

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
                await _downloader.DownloadPackage(configuration);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not download package.");
                throw new Commands.RunCommandHandler.AlreadyLoggedException();
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
                throw new Commands.RunCommandHandler.AlreadyLoggedException();
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
            _process.StartInfo.FileName =  Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");
            _process.StartInfo.CreateNoWindow = false;

            _process.StartInfo.Arguments = string.Join(" ", new string[] {
                "host",
                "--plebian-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new PlebianConfiguration { PackageType = packageType, PackageId = packageConfiguration.Id })))}\"",
                "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launcherConfiguration)))}\"",
            });
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
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not start ProcessHost process.");
                _stopped.SetCanceled(cancellationToken);
                return Task.CompletedTask;
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
