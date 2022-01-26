using Serilog;
using System.Diagnostics;

namespace fiskaltrust.Launcher.ServceInstallation
{
    public class LinuxSystemD
    {
        private static readonly string _servicePath = "/etc/systemd/system/";
        private readonly string _serviceName = "fiskaltrustLauncher";

        public LinuxSystemD(string? serviceName)
        {
            _serviceName = serviceName ?? _serviceName;
        }
        public async Task<int> InstallSystemD(string commandArgs, string? serviceDescription)
        {
            if (!IsSystemd())
            {
                return -1;
            }
            Log.Information("Installing service via systemd.");
            var serviceFileContent = GetServiceFileContent(serviceDescription ?? "Service installation of fiskaltrust launcher.", commandArgs);
            var serviceFilePath = Path.Combine(_servicePath, _serviceName+ ".service");
            await File.AppendAllLinesAsync(serviceFilePath, serviceFileContent).ConfigureAwait(false);
            RunProcess("systemctl", "daemon-reload");
            Log.Information("Starting service.");
            RunProcess("systemctl", "start " + _serviceName);
            Log.Information("Enable service.");
            return RunProcess("systemctl", "enable " + _serviceName);
        }
        public int UninstallSystemD()
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
        private static int RunProcess(string? fileName, string arguments, string expectedOutput = "")
        {
            string output;
            using (Process process = new())
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
            if (!string.IsNullOrEmpty(expectedOutput))
            {
                var exitcode = CompareOutput(output, expectedOutput);
                Log.Information("Process output: {processOutput} (expected: {expectedOutput})", output, expectedOutput);
                return exitcode;
            }
            return 0;
        }
        private static int CompareOutput(string output, string expectedOutput)
        {
            return output.Contains(expectedOutput) ? 0 : 1;
        }
        private static string [] GetServiceFileContent(string serviceDescription, string commandArgs)
        {
            var processPath = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable");
            
            var command = $"{processPath} {commandArgs}";
            return new string[] { "[Unit]",
                                  "Description="+ serviceDescription ,
                                  "",
                                  "[Service]",
                                  "Type=simple",
                                  string.Format("ExecStart={0}", command),
                                  "",
                                  "[Install]",
                                  "WantedBy = multi-user.target"
                                };
        }
    }
}
