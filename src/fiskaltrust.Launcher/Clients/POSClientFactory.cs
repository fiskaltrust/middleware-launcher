using fiskaltrust.ifPOS.v1;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.Middleware.Interface.Client;
using fiskaltrust.Middleware.Interface.Client.Grpc;
using fiskaltrust.Middleware.Interface.Client.Http;
using fiskaltrust.Middleware.Interface.Client.Soap;
using Grpc.Core;
using Grpc.Net.Client;

namespace fiskaltrust.Launcher.Clients
{
    public class POSClientFactory : IClientFactory<IPOS>
    {
        private readonly LauncherConfiguration _launcherConfiguration;

        public POSClientFactory(LauncherConfiguration launcherConfiguration) => _launcherConfiguration = launcherConfiguration;

        public IPOS CreateClient(ClientConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var retryPolicyoptions = new RetryPolicyOptions
            {
                DelayBetweenRetries = configuration.DelayBetweenRetries != default ? configuration.DelayBetweenRetries : RetryPolicyOptions.Default.DelayBetweenRetries,
                Retries = configuration.RetryCount ?? RetryPolicyOptions.Default.Retries,
                ClientTimeout = configuration.Timeout != default ? configuration.Timeout : RetryPolicyOptions.Default.ClientTimeout
            };

            var isHttps = !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificatePath) || !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificateBase64);
            var sslValidationDisabled = _launcherConfiguration != null && _launcherConfiguration.SslValidation.HasValue && _launcherConfiguration.SslValidation.Value;

            return configuration.UrlType switch
            {
                "grpc" => GrpcPosFactory.CreatePosAsync(new GrpcClientOptions
                {
                    Url = new Uri(configuration.Url.Replace("grpc://", isHttps ? "https://" : "http://")),
                    RetryPolicyOptions = retryPolicyoptions,
                    ChannelOptions = new GrpcChannelOptions
                    {
                        Credentials = isHttps ? ChannelCredentials.SecureSsl : ChannelCredentials.Insecure,
                        HttpHandler = isHttps && sslValidationDisabled ? new HttpClientHandler { ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true } : null
                    }
                }).Result,
                "rest" => HttpPosFactory.CreatePosAsync(new HttpPosClientOptions
                {
                    Url = new Uri(configuration.Url.Replace("rest://", isHttps ? "https://" : "http://")),
                    RetryPolicyOptions = retryPolicyoptions,
                }).Result,
                "http" or "https" or "net.tcp" => SoapPosFactory.CreatePosAsync(new ClientOptions
                {
                    Url = new Uri(configuration.Url),
                    RetryPolicyOptions = retryPolicyoptions
                }).Result,
                _ => throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC, REST and SOAP communication."),
            };
        }
    }
}
