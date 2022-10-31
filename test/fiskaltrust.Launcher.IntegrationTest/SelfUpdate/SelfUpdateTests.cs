using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Commands;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.IntegrationTest.Helpers;
using FluentAssertions;

namespace fiskaltrust.Launcher.IntegrationTest.SelfUpdate
{
    public class SelfUpdateTests
    {
        [Fact]
        public async Task Test()
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

            try
            {
                var lifetime = new TestLifetime();
                var launcherExecutablePath = new LauncherExecutablePath($"fiskaltrust.Launcher{(Runtime.Identifier.StartsWith("win") ? ".exe" : "")}");
                var runCommand = new RunCommandHandler(lifetime, new SelfUpdater(new LauncherProcessId(dummyProcess.Id), launcherExecutablePath), launcherExecutablePath)
                {
                    ArgsLauncherConfiguration = new LauncherConfiguration
                    {
                        CashboxId = launcherConfiguration.CashboxId,
                        AccessToken = launcherConfiguration.AccessToken,
                        ServiceFolder = launcherConfiguration.ServiceFolder,
                        Sandbox = true
                    },
                    LauncherConfigurationFile = $"{launcherConfiguration.ServiceFolder}/launcher.configuration.json"
                };

                var command = runCommand.InvokeAsync(null!);

                await lifetime.WaitForStartAsync(new CancellationToken());

                if (command.IsCompleted)
                {
                    throw new Exception(Directory.GetFiles("logs").Aggregate("", (acc, file) => acc + File.ReadAllText(file)));
                }

                foreach (string file in Directory.GetFiles("fiskaltrust.LauncherUpdater"))
                {
                    File.Copy(file, Path.Combine("service", launcherConfiguration.CashboxId.ToString()!, "fiskaltrust.Launcher", Path.GetFileName(file)), true);
                }

                lifetime.ApplicationLifetimeSource.StopApplication();

                var exitCode = await command;
                if (exitCode != 0) { throw new Exception($"Exitcode {exitCode}"); }
            }
            finally
            {
                dummyProcess.Kill();
            }

            try
            {
                var updaterProcess = Process.GetProcessesByName("fiskaltrust.LauncherUpdater").First();

                await updaterProcess.WaitForExitAsync();
            }
            catch { }

            var fvi = FileVersionInfo.GetVersionInfo($"fiskaltrust.Launcher{(Runtime.Identifier.StartsWith("win") ? ".exe" : "")}");
            try
            {

                new SemanticVersioning.Version(fvi.ProductVersion).Should().BeGreaterThanOrEqualTo(new SemanticVersioning.Version("2.0.0-preview1"));
            }
            catch
            {
                throw new Exception(Directory.GetFiles("logs").Aggregate("", (acc, file) => acc + File.ReadAllText(file)));
            }
        }
    }
}