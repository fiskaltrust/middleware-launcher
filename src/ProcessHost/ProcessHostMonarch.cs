using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{
    public class ProcessHostMonarcStartup : BackgroundService
    {
        private readonly ILogger<ProcessHostMonarch> _logger;
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ftCashBoxConfiguration _cashBoxConfiguration;

        public ProcessHostMonarcStartup(ILogger<ProcessHostMonarch> logger, Dictionary<Guid, ProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration)
        {
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
        }


        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // foreach (var helper in _cashBoxConfiguration.helpers)
            // {
            //     var host = new ProcessHostMonarch(loggerFactory.CreateLogger<ProcessHostMonarch>(), uri, helper.Id, helper, PackageType.Helper);
            //     hosts.Add(helper.Id, host);
            //     await host.Start(cancellationToken);
            // }
            foreach (var scu in _cashBoxConfiguration.ftSignaturCreationDevices)
            {
                var monarch = new ProcessHostMonarch(
                    _logger,
                    _launcherConfiguration,
                    scu,
                    PackageType.SCU);
                _hosts.Add(
                    scu.Id,
                    monarch
                );
                await monarch.Start(cancellationToken);
            }
            // foreach (var queue in _cashBoxConfiguration.ftQueues)
            // {
            //     var host = new ProcessHostMonarch(loggerFactory.CreateLogger<ProcessHostMonarch>(), uri, queue.Id, queue, PackageType.Queue);
            //     hosts.Add(queue.Id, host);
            //     await host.Start(cancellationToken);
            // }

            await Task.WhenAll(_hosts.Select(h => h.Value.Stopped()));

            return;
        }
    }

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;
        private readonly ILogger _logger;

        public ProcessHostMonarch(ILogger<ProcessHostMonarch> logger, LauncherConfiguration launcherConfiguration, PackageConfiguration configuration, PackageType packageType)
        {
            _logger = logger;

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
                        _process.Start(); // TODO add some kind of retry
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


            if (!_process.Start())
            {
                _stopped.SetCanceled(cancellationToken);
                return Task.CompletedTask;
            };

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
