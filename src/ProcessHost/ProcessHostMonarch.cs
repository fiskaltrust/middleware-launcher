using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{
    public class ProcessHostMonarcStartup : BackgroundService
    {
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ftCashBoxConfiguration _cashBoxConfiguration;
        private readonly ILogger _logger;

        public ProcessHostMonarcStartup(ILogger<ProcessHostMonarcStartup> logger, Dictionary<Guid, ProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration)
        {
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
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
            catch (Exception e)
            {
                _logger.LogError(e, "Error starting host.");
            }

            return;
        }

        private async Task StartProcessHostMonarch(PackageConfiguration configuration, PackageType packageType, CancellationToken cancellationToken)
        {
            var monarch = new ProcessHostMonarch(
                _launcherConfiguration,
                AddDefaultPackageConfig(configuration),
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
                throw;
            }
        }

        private PackageConfiguration AddDefaultPackageConfig(PackageConfiguration config)
        {
            config.Configuration.Add("cashboxid", _launcherConfiguration.CashboxId);
            config.Configuration.Add("accesstoken", _launcherConfiguration.AccessToken);
            config.Configuration.Add("useoffline", true);
            config.Configuration.Add("sandbox", _launcherConfiguration.Sandbox);
            config.Configuration.Add("configuration", JsonSerializer.Serialize(_cashBoxConfiguration));
            config.Configuration.Add("servicefolder", _launcherConfiguration.ServiceFolder);
            config.Configuration.Add("proxy", _launcherConfiguration.Proxy);

            foreach (var keyToRemove in config.Configuration.Where(c => c.Value == null).Select(c => c.Key).ToList())
            {
                config.Configuration.Remove(keyToRemove);
            }

            return config;
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

        public ProcessHostMonarch(LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PackageType packageType)
        {
            var executable = Environment.ProcessPath ?? throw new Exception("Could not find launcher .exe");

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = executable;
            _process.StartInfo.CreateNoWindow = false;

            _process.StartInfo.Arguments = string.Join(" ", new string[] {
                "host",
                "--plebian-config", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new PlebianConfiguration { PackageType = packageType })))}\"",
                "--launcher-config", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launcherConfiguration)))}\"",
                "--package-config", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packageConfiguration)))}\""
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
                _process.Kill();
            });

            _process.Exited += (sender, e) =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (_process.ExitCode != 0)
                    {
                        try
                        {
                            if (!_process.Start()) { throw new Exception(); }
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
                if (!_process.Start()) { throw new Exception(); }
            }
            catch
            {
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
