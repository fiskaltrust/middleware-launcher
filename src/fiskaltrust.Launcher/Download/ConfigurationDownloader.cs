using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
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

        public async Task DownloadConfigurationAsync(ECDiffieHellman clientEcdh)
        {
            if (_configuration.UseOffline!.Value) { return; }

            var clientPublicKey = Convert.ToBase64String(clientEcdh.PublicKey.ExportSubjectPublicKeyInfo());

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.ConfigurationUrl}api/configuration/{_configuration.CashboxId}"));
            request.Headers.Add("accesstoken", _configuration.AccessToken);
            request.Content = new StringContent($"{{ \"publicKeyX509\": \"{clientPublicKey}\" }}", Encoding.UTF8, "application/json");

            var response = await _httpClient!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var cashboxConfiguration = await response.Content.ReadFromJsonAsync<ftCashBoxConfiguration>();

            await File.WriteAllTextAsync(_configuration.CashboxConfigurationFile!, JsonSerializer.Serialize(cashboxConfiguration));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
