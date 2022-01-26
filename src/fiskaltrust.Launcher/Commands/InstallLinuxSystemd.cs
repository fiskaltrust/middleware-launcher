using Serilog;
using System.Diagnostics;

namespace fiskaltrust.Launcher.Commands
{
    public class InstallLinuxSystemd
    {
        private readonly string _launcherConfigurationFile;
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";
        private readonly string _serviceDescription = "Service installation of fiskaltrust launcher.";

        public InstallLinuxSystemd(string LauncherConfigurationFile, string? serviceName, string? serviceDescription)
        {
            _launcherConfigurationFile = LauncherConfigurationFile;
            _serviceName = serviceName ?? _serviceName;
            _serviceDescription = serviceDescription ?? _serviceDescription;
        }
        public async Task<int> InstallSystemd()
        {
            if (!IsSystemd())
            {
                return -1;
            }
            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileConten(_serviceDescription);
            var serviceFilePath = Path.Combine(_servicePath, _serviceName+ ".service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
            RunProcess("systemctl", "daemon-reload");
            Log.Information("Starting service.");
            RunProcess("systemctl", "start " + _serviceName);
            Log.Information("Enable service.");
            return RunProcess("systemctl", "enable " + _serviceName);
        }
        public int UninstallSystemd()
        {
            if (!IsSystemd())
            {
                return -1;
            }
            Log.Information("Stop service on systemd.");
            RunProcess("systemctl", "stop " + _serviceName);
            Log.Information("Disable service.");
            RunProcess("systemctl", "disable " + _serviceName);
            Log.Information("Remove service.");
            var serviceFilePath = Path.Combine(_servicePath, _serviceName + ".service");
            RunProcess("rm", serviceFilePath);
            Log.Information("Reload daemon.");
            RunProcess("systemctl", "daemon-reload");
            Log.Information("Reset failed.");
            return RunProcess("systemctl", "reset-failed");
        }
        private static bool IsSystemd()
        {
            var canInstall = RunProcess("ps", "--no-headers -o comm 1", "systemd");
            if (canInstall != 0)
            {
                Log.Error("Service installation works only for systemd setup.");
                return false;
            }
            return true;
        }
        private static int RunProcess(string? fileName, string arguments, string expectetOutput = "")
        {
            string output;
            using (Process process = new Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;   
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                StreamReader reader = process.StandardOutput;
                output = reader.ReadToEnd();
                process.WaitForExit();
            }
            if (!string.IsNullOrEmpty(expectetOutput))
            {
                var exitcode = CompareOutput(output, expectetOutput);
                Log.Information(string.Format("Output {0} und expectetOutput {1}", output, expectetOutput));
                return exitcode;
            }
            return 0;
        }
        private static int CompareOutput(string output, string expectetOutput)
        {
            return output.Contains(expectetOutput) ? 0 : 1;
        }
        private string [] GetServiceFileConten(string serviceDescription)
        {
            var processPath = $"{Environment.ProcessPath ?? throw new Exception("Could not find launcher executable")} run --launcher-configuration-file {_launcherConfigurationFile}";
            return new string[] { "[Unit]",
                                  "Description="+ serviceDescription ,
                                  "",
                                  "[Service]",
                                  "Type=simple",
                                  string.Format("ExecStart={0}", processPath),
                                  "",
                                  "[Install]",
                                  "WantedBy = multi-user.target"
                                };
        }
    }
}
