using fiskaltrust.Launcher.Common.Configuration;
using FluentAssertions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ConsolePlaceholder
{

    public  class ProcessHolder
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


        public void SendCtrC()
        {

            var launcherConfiguration = GetTestLauncherConfig();
            launcherConfiguration.LauncherVersion = new SemanticVersioning.Range("2.0.0-preview3");
            File.WriteAllText(Path.Combine(launcherConfiguration.ServiceFolder, "launcher.configuration.json"), JsonSerializer.Serialize(launcherConfiguration));
            var launcherExePath = Path.Combine(launcherConfiguration.ServiceFolder, "fiskaltrust.Launcher.exe");
            Console.WriteLine(launcherExePath);
            
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = launcherExePath;
            process.StartInfo.Arguments = $"run --cashbox-id {launcherConfiguration.CashboxId} --access-token {launcherConfiguration.AccessToken} --sandbox  --launcher-configuration-file {Path.Combine(launcherConfiguration.ServiceFolder, "launcher.configuration.json")}";
            Console.WriteLine(process.StartInfo.Arguments);
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

            output[0].Should().Contain("A new Launcher version is");

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
        }
        public static LauncherConfiguration GetTestLauncherConfig()
        {
            var launcherConfiguration = new LauncherConfiguration(true);
            launcherConfiguration.ServiceFolder = "C:\\source\\repos\\middleware-launcher\\test\\Result";
            launcherConfiguration.CashboxId = Guid.Parse("f3661e3c-5101-4d77-9396-c6cfc5d01a2c");
            launcherConfiguration.AccessToken = "BOQoYvuEFULhg/NFfkQ3kzrwOGdZRxgFYhjH59c8Fk93kA8EJeVRef013g3XZUq1cxJx6dDOGyi9QodBTDLHGpo=";
            launcherConfiguration.Sandbox = true;
            return launcherConfiguration;
        }
    }
}
