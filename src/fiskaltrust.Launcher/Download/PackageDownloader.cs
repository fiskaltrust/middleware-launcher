using System.IO.Compression;
using System.Security.Cryptography;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Download
{
    public sealed class PackageDownloader : IDisposable
    {
        private readonly LauncherConfiguration _configuration;
        private readonly ILogger<PackageDownloader>? _logger;
        private readonly HttpClient _httpClient;

        public PackageDownloader(ILogger<PackageDownloader>? logger, LauncherConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = new HttpClient(new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) });
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
            var name = $"{configuration.Package}-{configuration.Version}";
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");
            var sourcePath = Path.Combine(_configuration.ServiceFolder!, "cache", "packages", $"{name}.zip");

            if (File.Exists(targetName))
            {
                return;
            }

            if (Directory.Exists(targetPath)) { Directory.Delete(targetPath, true); }

            Directory.CreateDirectory(targetPath);


            for (var i = 0; i <= 1; i++)
            {
                if (!File.Exists(sourcePath))
                {
                    _logger?.LogInformation("Downloading package {name}.", name);
                    Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{configuration.Package}?version={configuration.Version}"));

                        request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                        request.Headers.Add("accesstoken", _configuration.AccessToken);

                        var response = await _httpClient.SendAsync(request);

                        response.EnsureSuccessStatusCode();

                        using var fileStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream);
                    }

                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{configuration.Package}/hash?version={configuration.Version}"));

                        request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                        request.Headers.Add("accesstoken", _configuration.AccessToken);

                        var response = await _httpClient.SendAsync(request);

                        response.EnsureSuccessStatusCode();
                        await File.WriteAllTextAsync($"{sourcePath}.hash", await response.Content.ReadAsStringAsync());
                    }
                }
                else
                {
                    _logger?.LogDebug("Found Package in cache.");
                }

                if(!await CheckHashAsync(sourcePath))
                {
                    File.Delete(sourcePath);
                    continue;
                }

                ZipFile.ExtractToDirectory(sourcePath, targetPath);

                if (!File.Exists(targetName))
                {
                    if (i == 0) { File.Delete(sourcePath); }
                    continue;
                }

                return;
            }

            throw new Exception("Downloaded Package is invalid");
        }

        public async Task<bool> CheckHashAsync(string sourcePath)
        {
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
            _httpClient.Dispose();
        }
    }
}
