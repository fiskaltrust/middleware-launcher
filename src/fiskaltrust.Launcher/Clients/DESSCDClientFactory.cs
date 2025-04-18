using fiskaltrust.ifPOS.v1.de;
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
    public class DESSCDClientFactory : IClientFactory<IDESSCD>
    {
        private readonly LauncherConfiguration _launcherConfiguration;

        public DESSCDClientFactory(LauncherConfiguration launcherConfiguration) => _launcherConfiguration = launcherConfiguration;

        public IDESSCD CreateClient(ClientConfiguration configuration)
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
            var sslValidationDisabled = _launcherConfiguration.SslValidation!.Value;

            return configuration.UrlType switch
            {
                "grpc" => GrpcDESSCDFactory.CreateSSCDAsync(new GrpcClientOptions
                {
                    Url = new Uri(configuration.Url.Replace("grpc://", isHttps ? "https://" : "http://")),
                    RetryPolicyOptions = retryPolicyoptions,
                    ChannelOptions = new GrpcChannelOptions
                    {
                        Credentials = isHttps ? ChannelCredentials.SecureSsl : ChannelCredentials.Insecure,
                        HttpHandler = isHttps && sslValidationDisabled ? new HttpClientHandler { ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true } : null
                    }
                }).Result,
                "rest" => HttpDESSCDFactory.CreateSSCDAsync(new HttpDESSCDClientOptions
                {
                    Url = new Uri(configuration.Url.Replace("rest://", isHttps ? "https://" : "http://")),
                    RetryPolicyOptions = retryPolicyoptions,
                    DisableSslValidation = sslValidationDisabled
                }).Result,
                "http" or "https" or "net.tcp" or "wcf" => SoapDESSCDFactory.CreateSSCDAsync(new ClientOptions
                {
                    Url = new Uri(configuration.Url),
                    RetryPolicyOptions = retryPolicyoptions
                }).Result,
                _ => throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC, REST and SOAP communication."),
            };
        }
    }
}
