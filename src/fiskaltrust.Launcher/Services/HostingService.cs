using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Grpc.Server;
using Serilog;
using System.Net;
using System.Reflection;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Interfaces;
using Microsoft.AspNetCore.HttpLogging;

namespace fiskaltrust.Launcher.Services
{
    public class HostingService : IAsyncDisposable
    {
        private readonly List<WebApplication> _hosts = new();
        private readonly PackageConfiguration _packageConfiguration;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly IProcessHostService? _processHostService;
        private readonly ILogger<HostingService> _logger;
        public HostingService(ILogger<HostingService> logger, PackageConfiguration packageConfiguration, LauncherConfiguration launcherConfiguration, IProcessHostService? processHostService = null)
        {
            _packageConfiguration = packageConfiguration;
            _launcherConfiguration = launcherConfiguration;
            _processHostService = processHostService;
            _logger = logger;
        }

        public async Task<WebApplication> HostService(Type T, Uri uri, HostingType hostingType, object instance, Action<WebApplication>? addEndpoints = null)
        {
            var builder = WebApplication.CreateBuilder();

            builder.Host.UseSerilog((hostingContext, services, loggerConfiguration) =>
                loggerConfiguration
                    .AddLoggingConfiguration(_launcherConfiguration, new[] { _packageConfiguration.Package, _packageConfiguration.Id.ToString() }, true)
                    .WriteTo.GrpcSink(_packageConfiguration, _processHostService));

            if (_launcherConfiguration.LogLevel == LogLevel.Debug)
            {
                builder.Services.AddHttpLogging(options =>
                options.LoggingFields =
                    HttpLoggingFields.RequestPath |
                    HttpLoggingFields.RequestMethod |
                    HttpLoggingFields.RequestScheme |
                    HttpLoggingFields.RequestQuery |
                    HttpLoggingFields.RequestBody |
                    HttpLoggingFields.ResponseStatusCode |
                    HttpLoggingFields.ResponseBody);
            }
            WebApplication app;

            switch (hostingType)
            {
                case HostingType.REST:
                    if (addEndpoints is null)
                    {
                        throw new ArgumentNullException(nameof(addEndpoints));
                    }
                    app = CreateHttpHost(builder, uri, addEndpoints);
                    break;
                case HostingType.GRPC:
                    app = (WebApplication)typeof(HostingService).GetTypeInfo().GetDeclaredMethod("CreateGrpcHost")!.MakeGenericMethod(new[] { T }).Invoke(this, new[] { builder, uri, instance })!;
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (_launcherConfiguration.LogLevel == LogLevel.Debug)
            {
                app.UseHttpLogging();
            }

            await app.StartAsync();

            _logger.LogInformation("Started {hostingType} hosting on {url}", hostingType.ToString(), uri.ToString().Replace("rest://", "http://"));
            return app;
        }

        public Task<WebApplication> HostService<T>(Uri uri, HostingType hostingType, T instance, Action<WebApplication>? addEndpoints = null) where T : class
            => HostService(typeof(T), uri, hostingType, instance, addEndpoints);

        private static WebApplication CreateHttpHost(WebApplicationBuilder builder, Uri uri, Action<WebApplication> addEndpoints)
        {
            var app = builder.Build();

            var url = new Uri(uri.ToString().Replace("rest://", "http://"));
            app.UsePathBase(url.AbsolutePath);
            app.Urls.Add(url.GetLeftPart(UriPartial.Authority));

            app.UseRouting();
            addEndpoints(app);

            return app;
        }

        internal static WebApplication CreateGrpcHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureKestrel(options => ConfigureKestrel(options, uri));
            builder.Services.AddCodeFirstGrpc();
            builder.Services.AddSingleton(instance);

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<T>());

            return app;
        }

        public static void ConfigureKestrel(KestrelServerOptions options, Uri uri)
        {
            if (uri.IsLoopback && uri.Port != 0)
            {
                options.ListenLocalhost(uri.Port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            }
            else if (IPAddress.TryParse(uri.Host, out var ip))
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
        }

#pragma warning disable CA1816
        public async ValueTask DisposeAsync()
        {
            foreach (var host in _hosts)
            {
                await host.StopAsync();
            }
        }
#pragma warning restore CA1816
    }
}
