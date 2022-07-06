using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Download
{
    public sealed class PackageDownloader : IDisposable
    {
        private readonly LauncherConfiguration _configuration;
        private readonly ILogger<PackageDownloader>? _logger;
        private readonly HttpClient? _httpClient;

        public PackageDownloader(ILogger<PackageDownloader>? logger, LauncherConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            if (!configuration.UseOffline!.Value)
            {
                _httpClient = new HttpClient(new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) });
            }
        }

        public string GetPackagePath(PackageConfiguration configuration)
        {
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");

            if (File.Exists(targetName))
            {
                return targetName;
            }
            else
            {
                throw new Exception("Could not find Package.");
            }
        }

        public async Task DownloadPackageAsync(PackageConfiguration configuration)
        {
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");
            await DownloadAsync(configuration.Package, configuration.Version, "undefined", targetPath, new[] { targetName });
        }

        private const string LAUNCHER_NAME = "fiskaltrust.Launcher";
        public async Task DownloadLauncherAsync()
        {
            string runtimeIdentifier = Environment.Is64BitProcess ? "x64" : "x86";
            if (OperatingSystem.IsWindows())
            {
                runtimeIdentifier = $"win-{runtimeIdentifier}";
            }
            else if (OperatingSystem.IsLinux())
            {
                runtimeIdentifier = $"linux-{runtimeIdentifier}";
            }
            else if (OperatingSystem.IsMacOS())
            {
                runtimeIdentifier = $"osx-{runtimeIdentifier}";
            }
            else
            {
                runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
            }

            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, LAUNCHER_NAME);

            await DownloadAsync(LAUNCHER_NAME, _configuration.LauncherVersion!.ToString(), runtimeIdentifier, targetPath, new[]
            {
                Path.Combine(targetPath, $"{LAUNCHER_NAME}{(OperatingSystem.IsWindows() ? ".exe" : "")}"),
                Path.Combine(targetPath, $"{LAUNCHER_NAME}Updater{(OperatingSystem.IsWindows() ? ".exe" : "")}"),
            });
        }

        private async Task DownloadAsync(string name, string version, string platform, string targetPath, IEnumerable<string> targetNames)
        {
            var combinedName = $"{name}-{version}";
            var sourcePath = Path.Combine(_configuration.ServiceFolder!, "cache", "packages", $"{combinedName}.zip");

            if (targetNames.Select(t => File.Exists(t)).All(t => t))
            {
                return;
            }

            if (Directory.Exists(targetPath)) { Directory.Delete(targetPath, true); }

            Directory.CreateDirectory(targetPath);


            for (var i = 0; i <= 1; i++)
            {
                if (!File.Exists(sourcePath))
                {
                    if (_configuration.UseOffline!.Value)
                    {
                        _logger?.LogWarning("Package {name} not found in download cache.", combinedName);
                        break;
                    }

                    _logger?.LogInformation("Downloading package {name}.", combinedName);
                    Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{name}?version={version}&platform={platform}"));

                        request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                        request.Headers.Add("accesstoken", _configuration.AccessToken);

                        var response = await _httpClient!.SendAsync(request);

                        response.EnsureSuccessStatusCode();

                        using var fileStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream);
                    }

                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{name}/hash?version={version}&platform={platform}"));

                        request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                        request.Headers.Add("accesstoken", _configuration.AccessToken);

                        var response = await _httpClient.SendAsync(request);

                        response.EnsureSuccessStatusCode();
                        await File.WriteAllTextAsync($"{sourcePath}.hash", await response.Content.ReadAsStringAsync());
                    }
                }
                else
                {
                    _logger?.LogDebug("Found package in download cache.");
                }

                if (!await CheckHashAsync(sourcePath))
                {
                    if (_configuration.UseOffline!.Value)
                    {
                        _logger?.LogWarning("File hash for {name} incorrect.", combinedName);
                    }
                    else
                    {
                        File.Delete(sourcePath);
                        continue;
                    }
                }

                ZipFile.ExtractToDirectory(sourcePath, targetPath);

                if (targetNames.Select(t => File.Exists(t)).Any(t => !t))
                {
                    if (_configuration.UseOffline!.Value)
                    {
                        _logger?.LogWarning("Package {name} did not contain the needed files.", combinedName);
                        break;
                    }

                    if (i == 0) { File.Delete(sourcePath); }
                    continue;
                }

                return;
            }

            throw new Exception("Downloaded package is invalid");
        }

        public async Task<bool> CheckHashAsync(string sourcePath)
        {
            if (!File.Exists($"{sourcePath}.hash"))
            {
                _logger?.LogWarning("Hash file not found");
                return false;
            }

            using FileStream stream = File.OpenRead(sourcePath);
            var computedHash = SHA256.Create().ComputeHash(stream);

            var hash = Convert.FromBase64String(await File.ReadAllTextAsync($"{sourcePath}.hash"));

            if (!computedHash.SequenceEqual(hash))
            {
                _logger?.LogWarning("Incorrect Hash");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
