using fiskaltrust.ifPOS.v1.de;
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

            return configuration.UrlType switch
            {
                "grpc" => GrpcDESSCDFactory.CreateSSCDAsync(new GrpcClientOptions { Url = new Uri(configuration.Url), RetryPolicyOptions = retryPolicyoptions, ChannelOptions = new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure } }).Result,
                "rest" => HttpDESSCDFactory.CreateSSCDAsync(new ClientOptions { Url = new Uri(configuration.Url.Replace("rest://", "http://")), RetryPolicyOptions = retryPolicyoptions }).Result,
                "http" or "https" or "net.tcp" => SoapDESSCDFactory.CreateSSCDAsync(new ClientOptions { Url = new Uri(configuration.Url), RetryPolicyOptions = retryPolicyoptions }).Result,
                _ => throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC, REST and SOAP communication."),
            };
        }
    }
}
