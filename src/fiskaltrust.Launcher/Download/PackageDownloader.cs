using System.IO.Compression;
using System.Security.Cryptography;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.storage.serialization.V0;
using Polly;
using Polly.Extensions.Http;

namespace fiskaltrust.Launcher.Download
{
    public sealed class PackageDownloader : IDisposable
    {
        private readonly PolicyHttpClient _policyHttpClient;

        private readonly LauncherConfiguration _configuration;
        private readonly ILogger<PackageDownloader>? _logger;
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public PackageDownloader(ILogger<PackageDownloader>? logger, LauncherConfiguration configuration,
            LauncherExecutablePath launcherExecutablePath)
        {
            _logger = logger;
            _configuration = configuration;
            _launcherExecutablePath = launcherExecutablePath;

            _policyHttpClient = new PolicyHttpClient(configuration, new HttpClient(new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) }));
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
            var targetName = $"{configuration.Package}.dll";
            await DownloadAsync(configuration.Package, configuration.Version, "undefined", targetPath, new[] { targetName });
        }

        public const string LAUNCHER_NAME = "fiskaltrust.Launcher";
        public async Task DownloadLauncherAsync(SemanticVersioning.Version version)
        {
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, LAUNCHER_NAME);
            await DownloadAsync(LAUNCHER_NAME, version.ToString(), Constants.Runtime.Identifier, targetPath, new[]
            {
                $"{LAUNCHER_NAME}{(OperatingSystem.IsWindows() ? ".exe" : "")}",
                $"{LAUNCHER_NAME}Updater{(OperatingSystem.IsWindows() ? ".exe" : "")}",
            });
        }

        public async Task<SemanticVersioning.Version?> GetConcreteVersionFromRange(string name, SemanticVersioning.Range range, string platform)
        {
            try
            {
                return new SemanticVersioning.Version(range.ToString());
            }
            catch { }

            if (_configuration.UseOffline!.Value)
            {
                _logger?.LogWarning("Cannot get latest {package} version from SemVer Range in offline mode.", name);
                return null;
            }

            try
            {
                var response = await _policyHttpClient.SendAsync(() =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/packages/{name}?platform={platform}"));

                    request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                    request.Headers.Add("accesstoken", _configuration.AccessToken);

                    return request;
                });

                response.EnsureSuccessStatusCode();

                var versions = (await response.Content.ReadFromJsonAsync<IEnumerable<string>>())?.Select(v => new SemanticVersioning.Version(v)) ?? new List<SemanticVersioning.Version>();

                return range.MaxSatisfying(versions);
            }
            catch (Exception e)
            {
                _logger?.LogWarning(e, "Could not get latest {package} version from SemVer Range {range}", name, range);
                return null;
            }
        }

        public async Task DownloadAsync(string name, string version, string platform, string targetPath, IEnumerable<string> targetNames)
        {
            var combinedName = $"{name}-{version}";

            var sourcePath = Path.Combine(_configuration.PackageCache!, "packages", $"{combinedName}.zip");

            var versionFile = Path.Combine(targetPath, "version.txt");

            if (File.Exists(versionFile) && await File.ReadAllTextAsync(versionFile) == version && targetNames.Select(t => File.Exists(Path.Combine(targetPath, t))).All(t => t))
            {
                return;
            }

            for (var i = 0; i <= 1; i++)
            {
                if (Directory.Exists(targetPath)) { Directory.Delete(targetPath, true); }

                Directory.CreateDirectory(targetPath);
                await File.WriteAllTextAsync(versionFile, version);

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
                        var response = await _policyHttpClient.SendAsync(() =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{name}?version={version}&platform={platform}"));

                            request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                            request.Headers.Add("accesstoken", _configuration.AccessToken);

                            return request;
                        });

                        response.EnsureSuccessStatusCode();

                        await using var fileStream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream);
                    }

                    {
                        var response = await _policyHttpClient.SendAsync(() =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.PackagesUrl}api/download/{name}/hash?version={version}&platform={platform}"));

                            request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
                            request.Headers.Add("accesstoken", _configuration.AccessToken);

                            return request;
                        });

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
                    _logger?.LogWarning("File hash for {name} incorrect.", combinedName);
                    if (!_configuration.UseOffline!.Value)
                    {
                        File.Delete(sourcePath);
                        continue;
                    }
                }

                ZipFile.ExtractToDirectory(sourcePath, targetPath);

                if (targetNames.Any(t => !File.Exists(Path.Combine(targetPath, t))))
                {
                    _logger?.LogWarning("Package {name} did not contain the needed files.", combinedName);
                    if (_configuration.UseOffline!.Value)
                    {
                        break;
                    }

                    if (i == 0) { File.Delete(sourcePath); }
                    continue;
                }

                return;
            }
            if (!_configuration.UseOffline!.Value)
            {
                throw new Exception("Downloaded package is invalid");
            }
        }


        public void CopyPackagesToCache()
        {
            var sourcePath = Path.Combine(Path.GetDirectoryName(_launcherExecutablePath.Path)!, "packages");
            var destinationPath = Path.Combine(_configuration.PackageCache!, "packages");

            if (!Directory.Exists(sourcePath))
            {
                _logger?.LogDebug("No offline packages found");
                return;
            }

            Directory.CreateDirectory(destinationPath);


            foreach (var filePath in Directory.GetFiles(sourcePath, "*.zip")
                         .Concat(Directory.GetFiles(sourcePath, "*.hash")))
            {
                var fileName = Path.GetFileName(filePath);
                var destinationFilePath = Path.Combine(destinationPath, fileName);

                if (File.Exists(destinationFilePath))
                {
                    _logger?.LogDebug("Package {fileName} already exists in cache.", fileName);
                    continue;
                }
                File.Copy(filePath, destinationFilePath, true);


                _logger?.LogInformation("Copied package {fileName} to cache", fileName);
            }

        }

        public async Task<bool> CheckHashAsync(string sourcePath)
        {
            if (!File.Exists($"{sourcePath}.hash"))
            {
                _logger?.LogWarning("Hash file not found.");
                return false;
            }

            using FileStream stream = File.OpenRead(sourcePath);
            var computedHash = SHA256.Create().ComputeHash(stream);

            var hash = Convert.FromBase64String(await File.ReadAllTextAsync($"{sourcePath}.hash"));

            if (!computedHash.SequenceEqual(hash))
            {
                _logger?.LogWarning("Incorrect Hash.");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            _policyHttpClient?.Dispose();
        }
    }
}
