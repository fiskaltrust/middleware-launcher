using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Commands;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.IntegrationTest.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fiskaltrust.Launcher.IntegrationTest.SelfUpdate
{
    public class SelfUpdateTests
    {
        public static async Task Test()
        {
            LauncherConfiguration launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig(Guid.Parse("c813ffc2-e129-45aa-8b51-9f2342bdfa08"), "BFHGxJScfQz7OJwIfH4QSYpVJj7mDkC4UYZQDiINXW6PED34hdJQ791wlFXKL+q3vPg/vYgaBSeB9oqyolQgtkE=");
            launcherConfiguration.LauncherVersion = new SemanticVersioning.Range("2.*.* || >=2.0.0-preview1");
            File.WriteAllText(Path.Combine(launcherConfiguration.ServiceFolder!, "launcher.configuration.json"), JsonSerializer.Serialize(launcherConfiguration));

            var dummyProcess = new Process();

            if (Runtime.Identifier.StartsWith("win"))
            {
                dummyProcess.StartInfo.FileName = "powershell.exe";
                dummyProcess.StartInfo.Arguments = "-NoProfile -C \"while($True) { sleep 10 }\"";
            }
            else
            {
                dummyProcess.StartInfo.FileName = "sh";
                dummyProcess.StartInfo.Arguments = "-c \"while true; do sleep 10; done\"";
            }

            dummyProcess.Start();
            DateTime updateStart;
            try
            {
                var lifetime = new TestLifetime();
                var launcherExecutablePath = new LauncherExecutablePath { Path = $"fiskaltrust.Launcher{(Runtime.Identifier.StartsWith("win") ? ".exe" : "")}" };

                var builder = Host.CreateDefaultBuilder().ConfigureServices(services =>
                    services
                        .AddSingleton<ILifetime>(lifetime)
                        .AddSingleton(launcherExecutablePath)
                        .AddSingleton(new LauncherProcessId(dummyProcess.Id))
                    );

                var runCommand = CommonHandler.HandleAsync<RunOptions, RunServices>(
                    new CommonOptions(
                        new LauncherConfiguration
                        {
                            CashboxId = launcherConfiguration.CashboxId,
                            AccessToken = launcherConfiguration.AccessToken,
                            ServiceFolder = launcherConfiguration.ServiceFolder,
                            Sandbox = true
                        },
                        $"{launcherConfiguration.ServiceFolder}launcher.configuration.json", "null",
                        true),
                    new RunOptions { },
                    builder.Build(),
                    RunHandler.HandleAsync);

                runCommand.Start();

                await lifetime.WaitForStartAsync(new CancellationToken());

                if (runCommand.IsCompleted)
                {
                    throw new Exception(Directory.GetFiles("logs").Aggregate("", (acc, file) => acc + File.ReadAllText(file)));
                }

                Directory.CreateDirectory(Path.Combine(launcherConfiguration.ServiceFolder!, launcherConfiguration.CashboxId.ToString()!, "fiskaltrust.Launcher"));
                foreach (string file in Directory.GetFiles("fiskaltrust.LauncherUpdater"))
                {
                    File.Copy(file, Path.Combine(launcherConfiguration.ServiceFolder!, launcherConfiguration.CashboxId.ToString()!, "fiskaltrust.Launcher", Path.GetFileName(file)), true);
                }

                updateStart = DateTime.UtcNow;
                await lifetime.StopAsync(CancellationToken.None);

                var exitCode = await runCommand.WaitAsync(TimeSpan.MaxValue);
                if (exitCode != 0) { throw new Exception($"Exitcode {exitCode}\n{Directory.GetFiles("logs").Aggregate("", (acc, file) => acc + File.ReadAllText(file))}"); }

                foreach (string file in Directory.GetFiles("./"))
                {
                    File.Copy(file, Path.Combine(launcherConfiguration.ServiceFolder!, launcherConfiguration.CashboxId.ToString()!, "fiskaltrust.Launcher", Path.GetFileName(file)), true);
                }
            }
            finally
            {
                dummyProcess.Kill();
            }

            try
            {
                var updaterProcess = Process.GetProcessesByName("fiskaltrust.LauncherUpdater").First();

                await updaterProcess.WaitForExitAsync();

                Console.WriteLine(await updaterProcess.StandardOutput.ReadToEndAsync());
                Console.WriteLine(await updaterProcess.StandardError.ReadToEndAsync());
            }
            catch { }

            await Task.Delay(TimeSpan.FromSeconds(10));

            var launcherFileCreation = File.GetLastWriteTimeUtc($"fiskaltrust.Launcher{(Runtime.Identifier.StartsWith("win") ? ".exe" : "")}");

            if (launcherFileCreation < updateStart)
            {
                throw new Exception($"Launcher executable was not modified. {updateStart.ToLongTimeString()} {launcherFileCreation.ToLongTimeString()}");
            }
        }
    }
}