using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.Middleware.Interface.Client;
using fiskaltrust.Middleware.Interface.Client.Grpc;
using fiskaltrust.Middleware.Interface.Client.Http;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

namespace fiskaltrust.Launcher.Clients
{

    public class DESSCDClientFactory : IClientFactory<IDESSCD>
    {
        private const int DEFAULT_RETRIES = 2;

        public IDESSCD CreateClient(ClientConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var retryPolicyoptions = new RetryPolicyOptions
            {
                DelayBetweenRetries = TimeSpan.FromSeconds(5),
                Retries = configuration.RetryCount ?? DEFAULT_RETRIES,
                ClientTimeout = configuration.Timeout  // TODO configuration.Timeout != default ? configuration.Timeout : TimeSpan.FromSeconds(AppDomainSettings.ScuTimeout)
            };

            switch (configuration.UrlType)
            {
                case "grpc":
                    var grpcUrl = configuration.Url.Replace("grpc://", "http://");
                    //var proxy = GrpcDESSCDFactory.CreateSSCDAsync(new GrpcClientOptions { Url = new Uri(grpcUrl), RetryPolicyOptions = retryPolicyoptions, ChannelOptions = new GrpcChannelOptions { Credentials = ChannelCredentials.Insecure } }).Result;


                    //GrpcClientFactory.AllowUnencryptedHttp2 = true;
                    var channel = GrpcChannel.ForAddress(new Uri(grpcUrl));
                    var proxy = channel.CreateGrpcService<IDESSCD>();


                    proxy.EchoAsync(new ScuDeEchoRequest { Message = "Hello SCU!" }).Wait();
                    return proxy;
                case "rest":
                    var url = configuration.Url.Replace("rest://", "http://");
                    return HttpDESSCDFactory.CreateSSCDAsync(new ClientOptions { Url = new Uri(url), RetryPolicyOptions = retryPolicyoptions }).Result;
                default:
                    throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC and HTTP communication.");
            }
        }
    }
}
