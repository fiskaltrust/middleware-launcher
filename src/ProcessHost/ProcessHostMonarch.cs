using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{

    public class ProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;

        public ProcessHostMonarch(Uri monarchUri, Guid id, PackageConfiguration configuration, PackageType packageType)
        {
            var executable = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? throw new Exception("Could not find launcher .exe");
            if(executable.EndsWith(".dll")) {
                executable = $"{executable[0..(executable.Length - 4)]}.exe";
            }
            _process = new Process();
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.FileName = executable;
            _process.StartInfo.CreateNoWindow = false;
            _process.StartInfo.Arguments = $"host --id \"{id}\" --package-type {packageType} --package-config \"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)))}\" --monarch-uri \"{monarchUri}\"";
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
