using fiskaltrust.Launcher.Constants;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Grpc.Server;
using System.Net;
using System.Reflection;

namespace fiskaltrust.Launcher.Services
{
    public class HostingService : IAsyncDisposable
    {
        private readonly List<WebApplication> _hosts = new();

        public async Task<WebApplication> HostService(Type T, Uri uri, HostingType hostingType, object instance, Action<WebApplication>? addEndpoints = null)
        {
            switch(hostingType)
            {
                case HostingType.REST:
                    if(addEndpoints == null)
                    {
                        throw new ArgumentNullException(nameof(addEndpoints));
                    }
                    return await CreateHttpHost(uri, addEndpoints);
                case HostingType.GRPC:
                    return await (Task<WebApplication>)typeof(HostingService).GetTypeInfo().GetDeclaredMethod("CreateGrpcHost")!.MakeGenericMethod(new[] { T }).Invoke(this, new[] { uri, instance })!;
                default:
                    throw new NotImplementedException();
            }
        }

        public Task<WebApplication> HostService<T>(Uri uri, HostingType hostingType, T instance, Action<WebApplication>? addEndpoints = null) where T : class
            => HostService(typeof(T), uri, hostingType, instance, addEndpoints);

        private static async Task<WebApplication> CreateHttpHost(Uri uri, Action<WebApplication> addEndpoints)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.AddConsole();
            var app = builder.Build();

            var url = new Uri(uri.ToString().Replace("rest://", "http://"));
            app.UsePathBase(url.AbsolutePath);
            app.Urls.Add(url.GetLeftPart(UriPartial.Authority));

            addEndpoints(app);

            await app.StartAsync();
            Console.WriteLine($"Started HTTP service on {url}");

            return app;
        }

        internal static async Task<WebApplication> CreateGrpcHost<T>(Uri uri, T instance) where T : class
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (uri.IsLoopback && uri.Port != 0)
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
