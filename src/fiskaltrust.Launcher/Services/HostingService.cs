using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Grpc.Server;
using Serilog;
using System.Net;
using System.Reflection;
using fiskaltrust.Launcher.Common.Configuration;
using Microsoft.AspNetCore.HttpLogging;
using fiskaltrust.Launcher.Services.Interfaces;
using Microsoft.AspNetCore.Http.Json;
using fiskaltrust.Launcher.Helpers;
using CoreWCF.Configuration;
using CoreWCF;
using fiskaltrust.ifPOS.v1;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace fiskaltrust.Launcher.Services
{
    public class HostingService
    {
        private readonly PackageConfiguration _packageConfiguration;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly IProcessHostService? _processHostService;
        private readonly ILogger<HostingService> _logger;

        private readonly long _messageSize = 16 * 1024 * 1024;
        private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _receiveTimeout = TimeSpan.FromDays(20);

        public HostingService(ILogger<HostingService> logger, PackageConfiguration packageConfiguration, LauncherConfiguration launcherConfiguration, IProcessHostService? processHostService = null)
        {
            _packageConfiguration = packageConfiguration;
            _launcherConfiguration = launcherConfiguration;
            _processHostService = processHostService;
            _logger = logger;

            if (packageConfiguration.Configuration.TryGetValue("messagesize", out var messageSizeStr) && long.TryParse(messageSizeStr.ToString(), out var messageSize))
            {
                _messageSize = messageSize;
            }
            if (packageConfiguration.Configuration.TryGetValue("timeout", out var timeoutStr) && long.TryParse(timeoutStr.ToString(), out var timeout))
            {
                _sendTimeout = TimeSpan.FromSeconds(timeout);
            }
        }        

        public async Task<WebApplication> HostService<T>(Uri uri, HostingType hostingType, T instance, Action<WebApplication, object> addEndpoints) where T : class
        {
            var builder = WebApplication.CreateBuilder();

            builder.Host.UseSerilog((_, __, loggerConfiguration) =>
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
                    app = CreateRestHost(builder, uri, instance, addEndpoints);
                    break;
                case HostingType.GRPC:
                    app = CreateGrpcHost(builder, uri, instance)!;
                    break;
                case HostingType.SOAP:
                    app = CreateSoapHost(builder, uri, instance)!;
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

        private static WebApplication CreateRestHost<T>(WebApplicationBuilder builder, Uri uri, T instance, Action<WebApplication, object> addEndpoints)
        {
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
                options.SerializerOptions.Converters.Add(new NumberToStringConverter());
            });

            var app = builder.Build();

            var url = new Uri(uri.ToString().Replace("rest://", "http://"));
            app.UsePathBase(url.AbsolutePath);
            app.Urls.Add(url.GetLeftPart(UriPartial.Authority));

            app.UseRouting();
            addEndpoints(app, instance);

            return app;
        }

        private WebApplication CreateSoapHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                ConfigureKestrel(options, uri, _ => { });
                options.AllowSynchronousIO = true;
            });

            // Add WSDL support
            builder.Services.AddServiceModelServices().AddServiceModelMetadata();
            builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

            // Instance will automatically be pulled from the DI container
            builder.Services.AddSingleton(instance.GetType(), _ => instance);

            var app = builder.Build();

            app.UseServiceModel(builder =>
            {
                builder.AddService(instance.GetType(), serviceOptions => serviceOptions.DebugBehavior.IncludeExceptionDetailInFaults = true);

                switch (uri.Scheme)
                {
                    case "http":
                        builder.AddServiceEndpoint(instance.GetType(), typeof(T), CreateBasicHttpBinding(BasicHttpSecurityMode.None), uri, null);
                        break;
                    case "https":
                        builder.AddServiceEndpoint(instance.GetType(), typeof(T), CreateBasicHttpBinding(BasicHttpSecurityMode.Transport), uri, null);
                        break;
                    case "net.tcp":
                        builder.AddServiceEndpoint(instance.GetType(), typeof(T), CreateNetTcpBinding(), uri, null);
                        break;
                    default:
                        throw new Exception();
                };
            });

            // Enable clients to request the WSDL file
            var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
            serviceMetadataBehavior.HttpGetEnabled = true;

            return app;
        }


        private NetTcpBinding CreateNetTcpBinding()
        {
            var binding = new NetTcpBinding(SecurityMode.None);
            if (_messageSize == 0)
            {
                binding.TransferMode = TransferMode.Streamed;
                binding.MaxReceivedMessageSize = long.MaxValue;
            }
            else
            {
                binding.MaxReceivedMessageSize = _messageSize;
            }
            binding.SendTimeout = _sendTimeout;
            binding.ReceiveTimeout = _receiveTimeout;
            return binding;
        }

        private BasicHttpBinding CreateBasicHttpBinding(BasicHttpSecurityMode securityMode)
        {
            var binding = new BasicHttpBinding(securityMode);
            if (_messageSize == 0)
            {
                binding.TransferMode = TransferMode.Streamed;
                binding.MaxReceivedMessageSize = long.MaxValue;
            }
            else
            {
                binding.MaxReceivedMessageSize = _messageSize;
            }
            binding.SendTimeout = _sendTimeout;
            binding.ReceiveTimeout = _receiveTimeout;
            return binding;
        }

        internal static WebApplication CreateGrpcHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureKestrel(options => ConfigureKestrelForGrpc(options, uri));
            builder.Services.AddCodeFirstGrpc();
            builder.Services.AddSingleton(instance);

            var app = builder.Build();

            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<T>());

            return app;
        }
        
        private static void ConfigureKestrel(KestrelServerOptions options, Uri uri, Action<ListenOptions> configureListeners)
        {
            if (uri.IsLoopback && uri.Port != 0)
            {
                options.ListenLocalhost(uri.Port, configureListeners);
            }
            else if (IPAddress.TryParse(uri.Host, out var ip))
            {
                options.Listen(new IPEndPoint(ip, uri.Port), configureListeners);
            }
            else
            {
                options.ListenAnyIP(uri.Port, configureListeners);
            }
        }

        public static void ConfigureKestrelForGrpc(KestrelServerOptions options, Uri uri) => ConfigureKestrel(options, uri, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    }
}
