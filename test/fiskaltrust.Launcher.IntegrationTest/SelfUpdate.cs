using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Download;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace fiskaltrust.Launcher.IntegrationTest
{
    public class SelfUpdate
    {
        internal const int CTRL_C_EVENT = 0;
        [DllImport("kernel32.dll")]
        internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);
        // Delegate type to be used as the Handler Routine for SCCH
        delegate Boolean ConsoleCtrlDelegate(uint CtrlType);

        [Fact]
        public async Task TestSelfUpdate_SpecificVersion_Updated()
        {
            /*

            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            launcherConfiguration.LauncherVersion = new SemanticVersioning.Range("2.0.0-preview3");
            await File.WriteAllTextAsync(Path.Combine(launcherConfiguration.ServiceFolder, "launcher.configuration.json"), JsonSerializer.Serialize(launcherConfiguration));
            var launcherExePath = Path.Combine(launcherConfiguration.ServiceFolder,PackageDownloader.LAUNCHER_NAME + ".exe");

            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = launcherExePath;
            process.StartInfo.Arguments = $"run --cashbox-id {launcherConfiguration.CashboxId} --access-token {launcherConfiguration.AccessToken}";
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();


            var output = new List<string>();
            var read = process.StandardOutput.ReadLine();
            var linesRead = 0;
            while (!read.Contains("Press CTRL+C to exit.")) 
            {
                output.Add(read);
                read = process.StandardOutput.ReadLine();
                linesRead++;
            }

            output[0].Should().Contain("A new Launcher version is set.");

            FreeConsole();
            if (AttachConsole((uint)process.Id))
            {
                SetConsoleCtrlHandler(null, true);
                try
                {
                    if (GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0))
                    {
                        process.WaitForExit();
                    }
                }
                finally
                {
                    SetConsoleCtrlHandler(null, false);
                    FreeConsole();
                }
            }
            */

            var consolePlaceholder = new Process();
            var consolePrj = AppDomain.CurrentDomain.BaseDirectory.Replace("fiskaltrust.Launcher.IntegrationTest", "ConsolePlaceholder");
            var placeholderExe = Path.Combine(consolePrj, "ConsolePlaceholder.exe");
            consolePlaceholder.StartInfo.FileName = placeholderExe;
            consolePlaceholder.Start();

            consolePlaceholder.WaitForExit();


            await Task.Delay(1000);

            while (Process.GetProcessesByName("fiskaltrust.Launcher").Length > 0)
            {
                await Task.Delay(500);
            }

            while (Process.GetProcessesByName("fiskaltrust.LauncherUpdater.exe").Length > 0)
            {
                await Task.Delay(500);
            }

            var fvi = FileVersionInfo.GetVersionInfo(Path.Combine("C:\\source\\repos\\middleware-launcher\\test\\Result", PackageDownloader.LAUNCHER_NAME + ".exe"));

            fvi.ProductVersion.Should().Be("2.0.0-preview3");

        }
    }
}

