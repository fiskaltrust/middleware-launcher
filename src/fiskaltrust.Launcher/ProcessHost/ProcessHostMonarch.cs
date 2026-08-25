using System.Diagnostics;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.storage.serialization.V0;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.ProcessHost
{
    public interface IProcessHostMonarch
    {
        public Task Start(CancellationToken cancellationToken);
        public void SetPlebeanStarted();
        public void SetStartupCompleted();
        public Task GetStopped();
    }

    public class ProcessHostMonarch : IProcessHostMonarch
    {
        private Process? _process;
        private TaskCompletionSource _started;
        private bool _monarchStartupCompleted;
        private TaskCompletionSource _stopped;
        private TimeSpan _restartDelay = TimeSpan.FromSeconds(1);

        private readonly PackageConfiguration _packageConfiguration;
        private readonly ILogger<ProcessHostMonarch> _logger;
        private readonly List<string> _plebeianLogBuffer = new List<string>();
        private LauncherConfiguration _launcherConfiguration;
        private PackageType _packageType;
        private LauncherExecutablePath _launcherExecutablePath;


        public ProcessHostMonarch(ILogger<ProcessHostMonarch> logger, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PackageType packageType, LauncherExecutablePath launcherExecutablePath)
        {
            _packageConfiguration = packageConfiguration;
            _launcherConfiguration = launcherConfiguration;
            _packageType = packageType;
            _launcherExecutablePath = launcherExecutablePath;
            _logger = logger;

            _monarchStartupCompleted = false;

            _stopped = new TaskCompletionSource();
            _started = new TaskCompletionSource();
        }

        private void Setup()
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = _launcherExecutablePath.Path,
                    CreateNoWindow = false,
                    Arguments = string.Join(" ", [
                        "host",
                        "--plebeian-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new PlebeianConfiguration { PackageType = _packageType, PackageId = _packageConfiguration.Id }.Serialize()))}\"",
                        "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_launcherConfiguration.Serialize()))}\"",
                    ]),
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += ReceiveStdOut;
            _process.ErrorDataReceived += ReceiveStdOut;

            //if (Debugger.IsAttached && _packageType == PackageType.Queue)
            //{
            //    _process.StartInfo.Arguments += " --debugging";
            //}
        }

        private void ReceiveStdOut(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                _plebeianLogBuffer.Add(e.Data);
            }
        }

        private void HandleCancellation()
        {
            _logger.HandlingCancellation(_packageConfiguration.Package, _packageConfiguration.Id.ToString());

            try
            {
                if (_process is not null && !_process.HasExited)
                {
                    _logger.KillingPackage(_packageConfiguration.Package, _packageConfiguration.Id.ToString());
                    _process.Kill();
                }
            }
            catch { }
        }

        private async Task HandleExit(CancellationToken cancellationToken)
        {

            if (cancellationToken.IsCancellationRequested /* || (_process is not null && _process.ExitCode == 0) */) // Until https://github.com/dotnet/runtime/issues/67146 is addressed we cannot check the exit code.
            {
                _logger.HostHasShutdown(_packageConfiguration.Package, _packageConfiguration.Id.ToString());
                // Cancellation was requested, and the process has exited.
                // Or the process has exited gracefully.
                _started.TrySetResult();
                _stopped.SetResult();
                return;
            }

            // if (_process is not null && _process.ExitCode != 0)
            {
                _logger.HostHasCrashed(_packageConfiguration.Package, _packageConfiguration.Id.ToString());
            }

            // Cancellation was not requested, and the process has exited erroniously.
            if (!_started.Task.IsCompleted)
            {
                // If the process hat not signaled startup, we print the log buffer.
                _logger.ErrorWhileStartingPackage(_packageConfiguration.Package, _packageConfiguration.Id.ToString(), string.Join(Environment.NewLine, _plebeianLogBuffer));
                _plebeianLogBuffer.Clear();
            }

            // Plebeian crash happened during the startup phase so we don't went to keep restarting.
            if (!_monarchStartupCompleted)
            {
                _started.TrySetCanceled();
                _stopped.TrySetCanceled();
                return;
            }

            _logger.BackingOffRestart(_restartDelay.ToString(), _packageConfiguration.Package, _packageConfiguration.Id.ToString());

            _restartDelay *= 2;
            try
            {
                await Task.Delay(_restartDelay, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            _logger.RestartingPackage(_packageConfiguration.Package, _packageConfiguration.Id.ToString());

            try
            {
                StartProcess(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.CouldNotStartProcessHost(ex, _packageConfiguration.Package, _packageConfiguration.Id.ToString());
                _started.TrySetResult();
                _stopped.TrySetCanceled();
            }

        }

        private void StartProcess(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) { return; }

            Setup();

            _process!.Exited += (_, _) => { var _ = Task.Run(async () => await HandleExit(cancellationToken)); };

            if (!_process!.Start()) { throw new Exception($"Process.Start() was false for {_packageConfiguration.Package} {_packageConfiguration.Id}"); }

            try
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch { }
        }


        public Task Start(CancellationToken cancellationToken)
        {
            cancellationToken.Register(HandleCancellation);

            try
            {
                StartProcess(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.CouldNotStartProcessHostGeneral(e);
                _stopped!.TrySetCanceled(cancellationToken);
                return Task.CompletedTask;
            }

            _logger.ProcessIdDebug(_process?.Id ?? 0);
            if (Debugger.IsAttached)
            {
                Debug.WriteLine($"ProcessId {_process?.Id}");
            }

            return _started.Task;
        }

        public void SetPlebeanStarted()
        {
            _started.TrySetResult();
            if (_process is not null)
            {
                _process.OutputDataReceived -= ReceiveStdOut;
                _process.ErrorDataReceived -= ReceiveStdOut;
            }
            _plebeianLogBuffer.Clear();
        }

        public void SetStartupCompleted()
        {
            _monarchStartupCompleted = true;
        }

        public Task GetStopped()
        {
            return _stopped.Task;
        }
    }
}
