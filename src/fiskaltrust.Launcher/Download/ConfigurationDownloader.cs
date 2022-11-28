using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Download
{
    public sealed class ConfigurationDownloader : IDisposable
    {
        private readonly LauncherConfiguration _configuration;
        private readonly HttpClient? _httpClient;

        public ConfigurationDownloader(LauncherConfiguration configuration)
        {
            _configuration = configuration;
            if (!configuration.UseOffline!.Value)
            {
                _httpClient = new HttpClient(new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) });
            }
        }

        public async Task<bool> DownloadConfigurationAsync()
        {
            if (_configuration.UseOffline!.Value)
            {
                return File.Exists(_configuration.CashboxConfigurationFile!);
            }

            var clientPublicKey = Convert.ToBase64String(CashboxConfigEncryption.CreateCurve().PublicKey.ExportSubjectPublicKeyInfo());

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.ConfigurationUrl}api/configuration/{_configuration.CashboxId}"));
            request.Headers.Add("accesstoken", _configuration.AccessToken);
            request.Content = new StringContent($"{{ \"publicKeyX509\": \"{clientPublicKey}\" }}", Encoding.UTF8, "application/json");

            var response = await _httpClient!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var cashboxConfiguration = await response.Content.ReadFromJsonAsync<ftCashBoxConfiguration>();
            await File.WriteAllTextAsync(_configuration.CashboxConfigurationFile!, cashboxConfiguration?.Serialize());

            return true;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
