using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Download;
using fiskaltrust.storage.serialization.V0;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net.Http.Json;
using FluentAssertions;
using System.IO;

namespace fiskaltrust.Launcher.IntegrationTest.Download
{
    public class PackageDownloaderTest
    {

        [Fact]
        public async Task DownloadPackageAsync_ValidDownload_DownloadedFiles()
        {
            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration);

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages"));
            var response = await httpClient!.SendAsync(request);
            var packages = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();

            foreach (var package in packages)
            {
                request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages/{package}"));
                response = await httpClient!.SendAsync(request);
                var versions = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
                var packageConfiguration = new PackageConfiguration()
                {
                    Id = Guid.NewGuid(),
                    Package = package,
                    Version = versions.Last()
                };
                try
                {
                    await packageDownloader.DownloadPackageAsync(packageConfiguration);
                }
                catch (HttpRequestException e)  {
                    throw new HttpRequestException($"Download of {package} version : {versions.Last()} failed!", e);
                }
                var path = Path.Combine(launcherConfiguration.ServiceFolder, "service", launcherConfiguration.CashboxId.ToString(), packageConfiguration.Id.ToString(), $"{package}.dll");
                _ = File.Exists(path).Should().BeTrue();

            }
        }

        [Fact]
        public async Task DownloadLauncherAsync_ActualLauncherVersion_DownloadedFiles()
        {
            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration);
            var platforms = new string[] {
                "win-x86",
                "win-x64",
                "linux-x86",
                "linux-x64",
                "osx-x86",
                "osx-x64",
            };

            var targetPath = Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId?.ToString()!, PackageDownloader.LAUNCHER_NAME);

            var httpClient = new HttpClient();
            foreach (var platform in platforms)
            {
                var platformPath = Path.Combine(targetPath, platform);

                if (Directory.Exists(platformPath)) {
                    Directory.Delete(platformPath, true);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages/{PackageDownloader.LAUNCHER_NAME}?platform={platform}"));
                var response = await httpClient!.SendAsync(request);
                var versions = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
                if (versions == null || !versions.Any())
                {
                    continue;
                }
                try
                {
                    await packageDownloader.DownloadAsync(PackageDownloader.LAUNCHER_NAME, versions.Last(), platform, platformPath, new[]{
                        $"{PackageDownloader.LAUNCHER_NAME}.exe",
                        $"{PackageDownloader.LAUNCHER_NAME}Updater.exe"
                    });
                }
                catch (HttpRequestException e)
                {
                    throw new HttpRequestException($"Download of {PackageDownloader.LAUNCHER_NAME} version : {versions.Last()} failed!", e);
                }

                _ = File.Exists(Path.Combine(platformPath, $"{PackageDownloader.LAUNCHER_NAME}.exe")).Should().BeTrue();
                _ = File.Exists(Path.Combine(platformPath, $"{PackageDownloader.LAUNCHER_NAME}Updater.exe")).Should().BeTrue();
            }

        }
        [Fact]
        public async Task GetConcreteVersionFromRange_()
        {
            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            launcherConfiguration.LauncherVersion = new SemanticVersioning.Range(">= 2.0.0-preview3");
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration);

            // ["2.0.0-beta.1.22185.54759","2.0.0-beta.1.22187.54788","2.0.0-beta.1.22187.54789","2.0.0-beta.1.22188.54846","2.0.0-beta.1.22189.54864","2.0.0-beta.1.22189.54870","2.0.0-beta.1.22189.54875","2.0.0-preview2","2.0.0-preview3.22248.55973","2.0.0-preview3.22249.56004","2.0.0-preview3"]

            var launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, launcherConfiguration.LauncherVersion, Constants.Runtime.Identifier);

            //ToDo comparison - Version nameing
        }



    }
}
