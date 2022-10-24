using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.Middleware.Interface.Client;
using fiskaltrust.Middleware.Interface.Client.Grpc;
using fiskaltrust.Middleware.Interface.Client.Http;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

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

            switch (configuration.UrlType)
            {
                case "grpc":
                    var proxy = GrpcDESSCDFactory.CreateSSCDAsync(new GrpcClientOptions { Url = new Uri(configuration.Url), RetryPolicyOptions = RetryPolicyOptions.Default }).Result;

                    proxy.EchoAsync(new ScuDeEchoRequest { Message = "Hello SCU!" }).Wait();
                    return proxy;
                case "rest":
                    var url = configuration.Url.Replace("rest://", "http://");
                    return HttpDESSCDFactory.CreateSSCDAsync(new ClientOptions { Url = new Uri(url), RetryPolicyOptions = RetryPolicyOptions.Default }).Result;
                default:
                    throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC and HTTP communication.");
            }
        }
    }
}
