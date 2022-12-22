using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProtoBuf.Grpc.Server;
using Serilog;
using System.Net;
using fiskaltrust.Launcher.Common.Configuration;
using Microsoft.AspNetCore.HttpLogging;
using fiskaltrust.Launcher.Services.Interfaces;
using Microsoft.AspNetCore.Http.Json;
using fiskaltrust.Launcher.Helpers;
using CoreWCF.Configuration;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using System.Security.Cryptography.X509Certificates;

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

        public async Task<WebApplication> HostService<T>(Uri uri, HostingType hostingType, T instance, Action<WebApplication, object>? addEndpoints) where T : class
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

            _logger.LogInformation("Started {hostingType} hosting on {url}", hostingType.ToString(), GetRestUri(uri));
            return app;
        }

        private WebApplication CreateRestHost<T>(WebApplicationBuilder builder, Uri uri, T instance, Action<WebApplication, object> addEndpoints)
        {
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
                options.SerializerOptions.Converters.Add(new NumberToStringConverter());
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                ConfigureKestrel(options, new Uri(GetRestUri(uri)), listenOptions => ConfigureTls(listenOptions));
                options.AllowSynchronousIO = true;
            });

            var app = builder.Build();
            app.UsePathBase(uri.AbsolutePath);

            app.UseRouting();
            addEndpoints(app, instance!);

            return app;
        }

        private string GetRestUri(Uri uri)
        {
            var isHttps = !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificatePath) || !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificateBase64);
            return uri.ToString().Replace("rest://", isHttps ? "https://" : "http://");
        }

        private WebApplication CreateSoapHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                ConfigureKestrel(options, uri, listenOptions => ConfigureTls(listenOptions));
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

        private WebApplication CreateGrpcHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureKestrel(options => ConfigureKestrelForGrpc(options, uri, listenOptions => ConfigureTls(listenOptions)));
            builder.Services.AddCodeFirstGrpc(options => options.EnableDetailedErrors = true);
            builder.Services.AddSingleton(instance);

            var app = builder.Build();

            app.UseRouting();
#pragma warning disable ASP0014
            app.UseEndpoints(endpoints => endpoints.MapGrpcService<T>());
#pragma warning restore ASP0014

            return app;
        }

        private void ConfigureTls(ListenOptions listenOptions)
        {
            if (!string.IsNullOrEmpty(_launcherConfiguration?.TlsCertificatePath))
            {
                if (File.Exists(_launcherConfiguration!.TlsCertificatePath) && Path.GetExtension(_launcherConfiguration!.TlsCertificatePath).ToLowerInvariant() == ".pfx")
                {
                    listenOptions.UseHttps(_launcherConfiguration!.TlsCertificatePath, _launcherConfiguration!.TlsCertificatePassword);
                }
                else
                {
                    _logger.LogError("A TLS certificate path was defined, but the file '{PfxPath}' does not exist or is not a valid PFX file.", _launcherConfiguration?.TlsCertificatePath);
                }
            }

            if (!string.IsNullOrEmpty(_launcherConfiguration?.TlsCertificateBase64))
            {
                try
                {
                    var cert = new X509Certificate2(Convert.FromBase64String(_launcherConfiguration!.TlsCertificateBase64), _launcherConfiguration!.TlsCertificatePassword);
                    listenOptions.UseHttps(cert);
                }
                catch (Exception ex)
                {
                    _logger.LogError("A TLS certificate was defined via base64 input, but could not be parsed. Error message: {TlsParsingError}", ex.Message);
                }
            }
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

        public static void ConfigureKestrelForGrpc(KestrelServerOptions options, Uri uri, Action<ListenOptions>? configureListeners = null) => ConfigureKestrel(options, uri, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
            configureListeners?.Invoke(listenOptions);
        });
    }
}
