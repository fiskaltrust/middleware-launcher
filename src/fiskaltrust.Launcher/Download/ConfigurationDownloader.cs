using System.Security.Cryptography;
using System.Text;
using fiskaltrust.Launcher.Common.Configuration;
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

        public async Task<string> GetConfigurationAsync(ECDiffieHellman clientCurve)
        {
            if (_configuration.UseOffline!.Value)
            {
                if (File.Exists(_configuration.CashboxConfigurationFile!))
                {
                    return await File.ReadAllTextAsync(_configuration.CashboxConfigurationFile!);
                }
                else
                {
                    throw new NoLocalConfig();
                }
            }

            var clientPublicKey = Convert.ToBase64String(clientCurve.PublicKey.ExportSubjectPublicKeyInfo());

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.ConfigurationUrl}api/configuration/{_configuration.CashboxId}"));
            request.Headers.Add("accesstoken", _configuration.AccessToken);
            request.Content = new StringContent($"{{ \"publicKeyX509\": \"{clientPublicKey}\" }}", Encoding.UTF8, "application/json");

            var response = await _httpClient!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<bool> DownloadConfigurationAsync(ECDiffieHellman clientCurve)
        {
            if (_configuration.UseOffline!.Value)
            {
                return File.Exists(_configuration.CashboxConfigurationFile!);
            }

            string cashboxConfiguration;

            try
            {
                cashboxConfiguration = await GetConfigurationAsync(clientCurve);
            }
            catch (NoLocalConfig)
            {
                return false;
            }

            await File.WriteAllTextAsync(_configuration.CashboxConfigurationFile!, cashboxConfiguration);

            return true;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class NoLocalConfig : Exception { }
}
