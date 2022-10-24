using fiskaltrust.ifPOS.v1;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.Middleware.Interface.Client;
using fiskaltrust.Middleware.Interface.Client.Grpc;
using fiskaltrust.Middleware.Interface.Client.Http;

namespace fiskaltrust.Launcher.Clients
{
    public class POSClientFactory : IClientFactory<IPOS>
    {
        public IPOS CreateClient(ClientConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            switch (configuration.UrlType)
            {
                case "grpc":
                    return GrpcPosFactory.CreatePosAsync(new GrpcClientOptions { Url = new Uri(configuration.Url), RetryPolicyOptions = RetryPolicyOptions.Default }).Result;
                case "rest":
                    var url = configuration.Url.Replace("rest://", "http://");
                    return HttpPosFactory.CreatePosAsync(new HttpPosClientOptions { Url = new Uri(url), RetryPolicyOptions = RetryPolicyOptions.Default }).Result;
                default:
                    throw new ArgumentException("This version of the fiskaltrust Launcher currently only supports gRPC and HTTP communication.");
            }
        }
    }
}
