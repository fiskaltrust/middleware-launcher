using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;
        private readonly ILogger _logger;

        public ProcessHostMonarch(ILogger logger, Uri monarchUri, Guid id, LauncherConfiguration launcherConfiguration, PackageConfiguration configuration, PackageType packageType)
        {
            _logger = logger;
            
            var executable = Environment.ProcessPath ?? throw new Exception("Could not find launcher .exe");

            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = executable;
            _process.StartInfo.CreateNoWindow = false;
            _process.StartInfo.Arguments = $"host --id \"{id}\" --package-type {packageType} --launcher-config \"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(launcherConfiguration)))}\" --package-config \"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)))}\" --monarch-uri \"{monarchUri}\"";
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.EnableRaisingEvents = true;
            _stopped = new TaskCompletionSource();
            _started = new TaskCompletionSource();
        }

        public Task Start(CancellationToken cancellationToken)
        {

            cancellationToken.Register(() => {
                _process.Kill();
            });

            _process.Exited += (sender, e) => {
                if(!cancellationToken.IsCancellationRequested) {
                    if(_process.ExitCode != 0) {
                        _process.Start(); // TODO add some kind of retry
                    } else {
                        _stopped.SetResult();
                        _started.SetCanceled();
                    }
                } else {
                    _stopped.SetResult();
                }
            };
            
            _process.OutputDataReceived += (sender, e) => {
                Console.WriteLine(e.Data);
            };

            _process.ErrorDataReceived += (sender, e) => {
                Console.WriteLine(e.Data);
            };


            if(!_process.Start()) {
                _stopped.SetCanceled(cancellationToken);
                return Task.CompletedTask;
            };

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            return _started.Task;
        }

        public void Started() {
            _started.SetResult();
        }
        public Task Stopped() {
            return _stopped.Task;
        }
    }
}
