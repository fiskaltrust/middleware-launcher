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
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public ProcessHostMonarcStartup(ILoggerFactory loggerFactory, ILogger<ProcessHostMonarcStartup> logger, Dictionary<Guid, ProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OS:        {OS}, {Bit}", Environment.OSVersion.VersionString, Environment.Is64BitOperatingSystem ? "64Bit" : "32Bit");
            _logger.LogInformation("CWD:       {CWD}", Path.GetFullPath("./"));
            _logger.LogInformation("CashBoxID: {CashBoxId}", _launcherConfiguration.CashboxId);

            foreach (var scu in _cashBoxConfiguration.ftSignaturCreationDevices)
            {
                await StartProcessHostMonarch(scu, PackageType.SCU, cancellationToken);
            }

            foreach (var queue in _cashBoxConfiguration.ftQueues)
            {
                await StartProcessHostMonarch(queue, PackageType.Queue, cancellationToken);
            }

            // foreach (var helper in _cashBoxConfiguration.helpers)
            // {
            //     await StartProcessHostMonarch(helper, PackageType.Helper, cancellationToken);
            // }

            try
            {
                await Task.WhenAll(_hosts.Select(h => h.Value.Stopped()));
            }
            catch (Exception e)
            {
                // _hosts.Select(h => h.Value.Stopped().Exception);
                _logger.LogError("{Message}", e.Message);
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

            await monarch.Start(cancellationToken);
        }

        private PackageConfiguration AddDefaultPackageConfig(PackageConfiguration config)
        {
            config.Configuration.Add("cashboxid", _launcherConfiguration.CashboxId);
            config.Configuration.Add("accesstoken", _launcherConfiguration.AccessToken);
            config.Configuration.Add("useoffline", true);
            config.Configuration.Add("sandbox", _launcherConfiguration.Sandbox);
            config.Configuration.Add("configuration", JsonSerializer.Serialize(_cashBoxConfiguration));
            config.Configuration.Add("servicefolder", _launcherConfiguration.ServiceFolder);

            return config;
        }
    }

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;

        public ProcessHostMonarch(LauncherConfiguration launcherConfiguration, PackageConfiguration configuration, PackageType packageType)
        {
            var executable = Environment.ProcessPath ?? throw new Exception("Could not find launcher .exe");

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = executable;
            _process.StartInfo.CreateNoWindow = false;
            _process.StartInfo.Arguments = $"host --package-type {packageType} --launcher-config \"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launcherConfiguration)))}\" --package-config \"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)))}\"";
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
                        }
                    }
                    else
                    {
                        _stopped.SetResult();
                        _started.SetCanceled();
                    }
                }
                else
                {
                    _stopped.SetResult();
                }
            };

            _process.OutputDataReceived += (sender, e) =>
            {
                Console.WriteLine(e.Data);
            };

            _process.ErrorDataReceived += (sender, e) =>
            {
                Console.WriteLine(e.Data);
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

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

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
