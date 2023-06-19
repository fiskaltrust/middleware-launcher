using System.Security.Cryptography;
using System.Text;
using fiskaltrust.Launcher.Common.Configuration;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;

namespace fiskaltrust.Launcher.Download
{
    public sealed class ConfigurationDownloader : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        private readonly LauncherConfiguration _configuration;

        public ConfigurationDownloader(LauncherConfiguration configuration)
        {
            _configuration = configuration;

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(configuration.DownloadRetry!.Value, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
            
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(configuration.DownloadTimeoutSec!.Value);

            _policy = Policy.WrapAsync(retryPolicy, timeoutPolicy);

            var httpClientHandler = new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) };
            _httpClient = new HttpClient(httpClientHandler);
        }
        
        // New constructor for testing
        public ConfigurationDownloader(LauncherConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(configuration.DownloadRetry!.Value, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(configuration.DownloadTimeoutSec!.Value);

            _policy = Policy.WrapAsync(retryPolicy, timeoutPolicy);
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

            var response = await _policy.ExecuteAsync(async ct => 
            {
                var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{_configuration.ConfigurationUrl}api/configuration/{_configuration.CashboxId}"));
                request.Headers.Add("accesstoken", _configuration.AccessToken);
                request.Content = new StringContent($"{{ \"publicKeyX509\": \"{clientPublicKey}\" }}", Encoding.UTF8, "application/json");

                return await _httpClient!.SendAsync(request, ct);
            }, CancellationToken.None);
    
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
            catch (NoLocalConfigException)
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

    public class NoLocalConfigException : Exception { }
}
