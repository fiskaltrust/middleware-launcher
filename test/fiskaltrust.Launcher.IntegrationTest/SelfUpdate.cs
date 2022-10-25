using fiskaltrust.Launcher.Commands;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.IntegrationTest.Download;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Net.Http.Json;

namespace fiskaltrust.Launcher.IntegrationTest
{
    public class SelfUpdate
    {
        [Fact]
        public async Task TestSelfUpdate_SpecificVersion_Updated()
        {
            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            var targetDir = Path.Combine(launcherConfiguration.ServiceFolder, "service", launcherConfiguration.CashboxId.ToString(), PackageDownloader.LAUNCHER_NAME);

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages/{PackageDownloader.LAUNCHER_NAME}?platform={Constants.Runtime.Identifier}"));
            var response = await httpClient!.SendAsync(request);
            var versions = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
            if (versions == null || !versions.Any() || versions.Count() <= 1)
            {
                return;
            }
            var secondLastVerion = versions.ToList()[versions.Count() - 2];
            await DownloadVersions(launcherConfiguration, targetDir, secondLastVerion);
            var chacheDir = Path.Combine(targetDir,"cache");
            await DownloadVersions(launcherConfiguration, chacheDir, versions.Last());

            var executionPath = Path.Combine(targetDir, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            var fvi = FileVersionInfo.GetVersionInfo(executionPath);
            fvi.ProductVersion.Should().Be(secondLastVerion);

            var runCommandHandler = new RunCommandHandler(Mock.Of<ILifetime>());
            launcherConfiguration.LauncherVersion = new SemanticVersioning.Range(versions.Last());
            runCommandHandler.LauncherConfiguration = launcherConfiguration;
            await runCommandHandler.StartLauncherUpdate(targetDir);

            while (Process.GetProcessesByName($"{PackageDownloader.LAUNCHER_NAME}Updater.exe").Any())
            {
                await Task.Delay(1000);
            }

            fvi = FileVersionInfo.GetVersionInfo(executionPath);

            fvi.ProductVersion.Should().Be(versions.Last());

        }

        private static async Task DownloadVersions(LauncherConfiguration launcherConfiguration, string targetDir, string version)
        {
            var executionPath = Path.Combine(targetDir, $"fiskaltrust.Launcher{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            var updaterPath = Path.Combine(targetDir, $"fiskaltrust.LauncherUpdater{(OperatingSystem.IsWindows() ? ".exe" : "")}");
            if (File.Exists(executionPath))
            {
                File.Delete(executionPath);
            }
            if (File.Exists(updaterPath))
            {
                File.Delete(updaterPath);
            }

            try
            {
                var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration);
                await packageDownloader.DownloadAsync(PackageDownloader.LAUNCHER_NAME, version, Constants.Runtime.Identifier, targetDir, new[]{
                        $"{PackageDownloader.LAUNCHER_NAME}.exe",
                        $"{PackageDownloader.LAUNCHER_NAME}Updater.exe"
                    });
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException($"Download of {PackageDownloader.LAUNCHER_NAME} version : {version} failed!", e);
            }

            _ = File.Exists(executionPath).Should().BeTrue($"File missing {executionPath}");
            _ = File.Exists(updaterPath).Should().BeTrue($"File missing {updaterPath}");
        }
    }
}

