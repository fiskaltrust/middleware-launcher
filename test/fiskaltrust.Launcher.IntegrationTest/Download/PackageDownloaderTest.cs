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
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration, new Launcher.Helpers.LauncherExecutablePath { Path = "" });

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages"));
            var response = await httpClient!.SendAsync(request);
            var packages = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();

            foreach (var package in packages!)
            {
                request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://packages-2-0-sandbox.fiskaltrust.cloud/api/packages/{package}"));
                response = await httpClient!.SendAsync(request);
                var versions = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
                var packageConfiguration = new PackageConfiguration()
                {
                    Id = Guid.NewGuid(),
                    Package = package,
                    Version = versions!.Last()
                };
                var path = Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId.ToString()!, packageConfiguration.Id.ToString(), $"{package}.dll");
                if (Directory.Exists(Path.GetDirectoryName(path)!))
                {
                    Directory.Delete(Path.GetDirectoryName(path)!, true);
                }
                try
                {
                    await packageDownloader.DownloadPackageAsync(packageConfiguration);
                }
                catch (HttpRequestException e)
                {
                    throw new HttpRequestException($"Download of {package} version : {versions!.Last()} failed!", e);
                }
                _ = File.Exists(path).Should().BeTrue();
                new FileInfo(path).Length.Should().BeGreaterThan(0);
                Directory.Delete(Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId.ToString()!, packageConfiguration.Id.ToString()), true);
            }
        }

        [Fact]
        public async Task DownloadLauncherAsync_ActualLauncherVersion_DownloadedFiles()
        {
            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig();
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration, new Launcher.Helpers.LauncherExecutablePath { Path = "" });
            var platforms = new string[] {
                "win-x86",
                "win-x64",
                "linux-x86",
                "linux-x64",
                "osx-x86",
                "osx-x64",
                // TODO: Add linux-arm and linux-arm64 after deployment went through
            };

            var targetPath = Path.Combine(launcherConfiguration.ServiceFolder!, "service", launcherConfiguration.CashboxId?.ToString()!, PackageDownloader.LAUNCHER_NAME);

            var httpClient = new HttpClient();
            foreach (var platform in platforms)
            {
                var platformPath = Path.Combine(targetPath, platform);

                if (Directory.Exists(platformPath))
                {
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
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(), launcherConfiguration, new Launcher.Helpers.LauncherExecutablePath { Path = "" });

            var launcherVersion = await packageDownloader.GetConcreteVersionFromRange(PackageDownloader.LAUNCHER_NAME, launcherConfiguration.LauncherVersion, Constants.Runtime.Identifier);

            //ToDo comparison - Version nameing
        }

        [Fact]
        public void CopyPackagesToCache_DummyPackages_CopiedToCache()
        {

            var tempServiceFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempPackageCache = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempServiceFolder);
            Directory.CreateDirectory(tempPackageCache);

            var launcherConfiguration = TestLauncherConfig.GetTestLauncherConfig(serviceFolder: tempServiceFolder, packageCache: tempPackageCache);
            var packageDownloader = new PackageDownloader(Mock.Of<ILogger<PackageDownloader>>(),
                launcherConfiguration, new Launcher.Helpers.LauncherExecutablePath { Path = Path.Combine(tempServiceFolder, "fiskaltrust.Launcher.exe") });

            var sourcePath = Path.Combine(tempServiceFolder, "packages");
            Directory.CreateDirectory(sourcePath);

            var packageFiles = Enumerable.Range(0, 5)
                .Select(i => $"package{i}_{DateTime.Now.Ticks}.zip")
                .ToList();

            packageFiles.ForEach(fileName =>
            {
                var filePath = Path.Combine(sourcePath, fileName);
                using var zipFile = File.Create(filePath);
            });

            try
            {
                packageDownloader.CopyPackagesToCache();

                packageFiles.ForEach(fileName =>
                {
                    var destinationFilePath = Path.Combine(tempPackageCache, "packages", fileName);
                    File.Exists(destinationFilePath).Should().BeTrue();
                });
            }
            finally
            {
                Directory.Delete(tempServiceFolder, true);
                Directory.Delete(tempPackageCache, true);
            }
        }

    }
}
