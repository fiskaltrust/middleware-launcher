
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Download
{
    public class Downloader : IDisposable
    {
        private readonly LauncherConfiguration _configuration;
        private readonly ILogger<Downloader>? _logger;
        private readonly HttpClient _httpClient;

        public Downloader(ILogger<Downloader>? logger, LauncherConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = new HttpClient(new HttpClientHandler { Proxy = CreateProxy(configuration.Proxy) });
        }

        private static WebProxy? CreateProxy(string? proxyString)
        {
            if (proxyString != null)
            {
                string address = string.Empty;
                bool bypasslocalhost = true;
                List<string> bypass = new();
                string username = string.Empty;
                string password = string.Empty;

                if (proxyString.ToLower() == "off")
                {
                    return new WebProxy();
                }
                else
                {

                    foreach (string keyvalue in proxyString.Split(new char[] { ';' }))
                    {
                        var data = keyvalue.Split(new char[] { '=' });
                        if (data.Length < 2)
                        {
                            continue;
                        }

                        switch (data[0].ToLower().Trim())
                        {
                            case "address": address = data[1]; break;
                            case "bypasslocalhost": if (!bool.TryParse(data[1], out bypasslocalhost)) { bypasslocalhost = false; } break;
                            case "bypass": bypass.Add(data[1]); break;
                            case "username": username = data[1]; break;
                            case "password": password = data[1]; break;
                            default: break;
                        }
                    }

                    WebProxy? proxy;

                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        proxy = new WebProxy(address, bypasslocalhost, bypass.ToArray());
                    }
                    else
                    {
                        return null;
                    }

                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        proxy.UseDefaultCredentials = false;
                        proxy.Credentials = new NetworkCredential(username, password);
                    }

                    return proxy;
                }
            }

            return null;
        }

        public string GetPackagePath(PackageConfiguration configuration)
        {
            var targetPath = Path.Combine(_configuration.ServiceFolder, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
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

        public async Task DownloadPackage(PackageConfiguration configuration)
        {
            var name = $"{configuration.Package}-{configuration.Version}";
            var targetPath = Path.Combine(_configuration.ServiceFolder, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");
            var sourcePath = Path.Combine(_configuration.ServiceFolder, "cache", "packages", $"{name}.zip");

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

                if(!await CheckHash(sourcePath))
                {
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

        public async Task<bool> CheckHash(string sourcePath)
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
        public async Task DownloadConfiguration()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.HelipadUrl}/api/configuration"));

            request.Headers.Add("cashboxid", _configuration.CashboxId.ToString());
            request.Headers.Add("accesstoken", _configuration.AccessToken);

            var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(responseString) ?? throw new Exception("Downloaded Configuration is Invalid");

            await File.WriteAllTextAsync(_configuration.CashboxConfigurationFile, JsonSerializer.Serialize(cashboxConfiguration));
        }

#pragma warning disable CA1816
        public void Dispose()
        {
            _httpClient.Dispose();
        }
#pragma warning restore CA1816
    }
}
