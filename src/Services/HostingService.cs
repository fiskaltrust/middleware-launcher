using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Grpc.Server;
using System.Net;

namespace fiskaltrust.Launcher.Services
{
    public class HostingService : IAsyncDisposable
    {
        private readonly List<WebApplication> _hosts = new();

        public async Task HostPackageAsync<T>(string[] uris, PackageType packageType, IServiceProvider serviceProvider) where T : class
        {
            foreach (var uri in uris)
            {
                var url = new Uri(uri);
                switch (url.Scheme)
                {
                    case "rest":
                        _hosts.Add(await CreateHttpHost<T>(url, packageType, serviceProvider));
                        break;
                    case "grpc":
                        switch(packageType) {
                            case PackageType.Queue:
                                _hosts.Add(await CreateGrpcHost(url, serviceProvider.GetRequiredService<IPOS>()));
                                break;
                            case PackageType.SCU:
                                _hosts.Add(await CreateGrpcHost(url, serviceProvider.GetRequiredService<IDESSCD>()));
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private static async Task<WebApplication> CreateHttpHost<T>(Uri uri, PackageType packageType, IServiceProvider serviceProvider)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.AddConsole();
            var app = builder.Build();

            var url = new Uri(uri.ToString().Replace("rest://", "http://"));
            app.UsePathBase(url.AbsolutePath);
            app.Urls.Add(url.GetLeftPart(UriPartial.Authority));

            switch (packageType)
            {
                case PackageType.Queue:
                    app.AddQueueEndpoints(serviceProvider.GetRequiredService<IPOS>());
                    break;
                case PackageType.SCU:
                    app.AddScuEndpoints(serviceProvider.GetRequiredService<IDESSCD>());
                    break;
                default:
                    break;
            }

            await app.StartAsync();
            Console.WriteLine($"Started HTTP service on {url}");

            return app;
        }

        private static async Task<WebApplication> CreateGrpcHost<T>(Uri uri, T instance) where T : class
        {
            var builder = WebApplication.CreateBuilder();

            //builder.Host.ConfigureWebHostDefaults(webBuilder =>
            //{
            //    webBuilder.UseUrls(url);
            //    // TODO this will fail on macOS: https://docs.microsoft.com/en-us/aspnet/core/grpc/troubleshoot?view=aspnetcore-5.0#unable-to-start-aspnet-core-grpc-app-on-macos
            //    //var isGrpcSslSupported = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version <= new Version(6, 1));
            //});

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (uri.IsLoopback)
                {
                    options.ListenLocalhost(uri.Port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                }
                else if(IPAddress.TryParse(uri.Host, out var ip))
                {
                    options.Listen(new IPEndPoint(ip, uri.Port), listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                }
                else
                {
                    options.ListenAnyIP(uri.Port, listenOptions =>
                    {
                        listenOptions.Protocols = HttpProtocols.Http2;
                    });
                }
            });
            builder.Services.AddCodeFirstGrpc();
            builder.Services.AddSingleton(instance);

            var app = builder.Build();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<T>());
            await app.StartAsync();

            return app;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var host in _hosts)
            {
                await host.StopAsync();
            }
            GC.SuppressFinalize(this);
        }
    }
}
