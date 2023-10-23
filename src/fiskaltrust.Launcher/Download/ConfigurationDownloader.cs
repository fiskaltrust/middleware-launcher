using System.Security.Cryptography;
using System.Text;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Helpers;
using Serilog;

namespace fiskaltrust.Launcher.Download
{
    public sealed class ConfigurationDownloader : IDisposable
    {
        private readonly PolicyHttpClient _policyHttpClient;
        private readonly LauncherConfiguration _configuration;

        public ConfigurationDownloader(LauncherConfiguration configuration)
            : this(configuration, new HttpClient(new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) }))
        {
        }

        public ConfigurationDownloader(LauncherConfiguration configuration, HttpClient httpClient)
        {

            _policyHttpClient = new PolicyHttpClient(configuration, httpClient);
            _configuration = configuration;
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
                    throw new NoLocalConfigException();
                }
            }

            var clientPublicKey = Convert.ToBase64String(clientCurve.PublicKey.ExportSubjectPublicKeyInfo());

            var response = await _policyHttpClient.SendAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.ConfigurationUrl}api/configuration/{_configuration.CashboxId}"));
                request.Headers.Add("accesstoken", _configuration.AccessToken);
                request.Content = new StringContent($"{{ \"publicKeyX509\": \"{clientPublicKey}\" }}", Encoding.UTF8, "application/json");

                return request;
            });

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }


        public async Task<bool> DownloadConfigurationAsync(ECDiffieHellman clientCurve)
        {
            Log.Verbose("Downloading Cashbox configuration.");

            if (_configuration.UseOffline!.Value)
            {
                return File.Exists(_configuration.CashboxConfigurationFile!);
            }

            string cashboxConfiguration;

            try
            {
                cashboxConfiguration = await GetConfigurationAsync(clientCurve);
            }
            catch (NoLocalConfigException)
            {
                return false;
            }

            await File.WriteAllTextAsync(_configuration.CashboxConfigurationFile!, cashboxConfiguration);

            return true;
        }

        public void Dispose()
        {
            _policyHttpClient?.Dispose();
        }
    }

    public class NoLocalConfigException : Exception { }
}
