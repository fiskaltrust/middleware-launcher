using System.Diagnostics;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.storage.serialization.V0;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.ProcessHost
{
    public interface IProcessHostMonarch
    {
        public Task Start(CancellationToken cancellationToken);

        public void Started();

        public Task Stopped();
    }

    public class ProcessHostMonarch : IProcessHostMonarch
    {
        private readonly Process _process;
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _stopped;
        private readonly PackageConfiguration _packageConfiguration;
        private readonly ILogger<ProcessHostMonarch> _logger;
        private readonly List<string> _plebianLogBuffer = new List<string>();

        public ProcessHostMonarch(ILogger<ProcessHostMonarch> logger, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PackageType packageType, LauncherExecutablePath launcherExecutablePath)
        {
            _packageConfiguration = packageConfiguration;
            _logger = logger;

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = launcherExecutablePath.Path,
                    CreateNoWindow = false,
                    Arguments = string.Join(" ", new string[] {
                        "host",
                        "--plebian-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new PlebianConfiguration { PackageType = packageType, PackageId = packageConfiguration.Id }.Serialize()))}\"",
                        "--launcher-configuration", $"\"{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(launcherConfiguration.Serialize()))}\"",
                    }),
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };
            
            // if (Debugger.IsAttached)
            // {
            //     _process.StartInfo.Arguments += " --debugging";
            // }

            _process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _plebianLogBuffer.Add(e.Data);
                }
            };

            _process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    _plebianLogBuffer.Add($"Plebian Error: {e.Data}");
                }
            };

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
                    try
                    {
                        if (_process.ExitCode != 0)
                        {
                            var errorMessage = string.Join("\n", _plebianLogBuffer);
                            _logger.LogError("Error when starting {package} {id}.\n{error}", _packageConfiguration.Package, _packageConfiguration.Id, errorMessage);
                            _started.TrySetResult();
                            _stopped.TrySetCanceled(cancellationToken);
                        }
                        else
                        {
                            _logger.LogInformation("Restarting {package} {id}.", _packageConfiguration.Package, _packageConfiguration.Id);
                            if (!_process.Start()) { throw new Exception($"Process.Start() was false for {_packageConfiguration.Package} {_packageConfiguration.Id}"); }

                            try
                            {
                                _process.BeginOutputReadLine();
                                _process.BeginErrorReadLine();
                            }
                            catch 
                            {
                                _logger.LogError("Error while initiating the output and error read lines.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not restart ProcessHost process for {package} {id}.", _packageConfiguration.Package, _packageConfiguration.Id);
                        _started.TrySetResult();
                        _stopped.TrySetCanceled(cancellationToken);
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
