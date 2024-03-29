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
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Server.HttpSys;
using System.Text.Json;
using fiskaltrust.ifPOS.v0;

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

        public async Task<WebApplication> HostService<T>(Uri uri, HostingType hostingType, T instance, Action<WebApplication>? addEndpoints) where T : class
        {
            var builder = WebApplication.CreateBuilder();

            // Configure Serilog for logging
            builder.Host.UseSerilog((_, __, loggerConfiguration) =>
                loggerConfiguration
                    .AddLoggingConfiguration(_launcherConfiguration, aspLogging: true)
                    .WriteTo.GrpcSink(_packageConfiguration, _processHostService));

            // Add HTTP logging if the log level is set to Debug or lower
            if (_launcherConfiguration.LogLevel <= LogLevel.Debug)
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

            // Check if UseHttpSysBinding is enabled and log warnings if necessary
            if (_launcherConfiguration.UseHttpSysBinding!.Value)
            {
                const string message = $"The configuration parameter {{parametername}} will be ignored because {nameof(_launcherConfiguration.UseHttpSysBinding)} is enabled.";
                if (_launcherConfiguration.TlsCertificateBase64 is not null)
                {
                    _logger.LogWarning(message, nameof(_launcherConfiguration.TlsCertificateBase64));
                }

                if (_launcherConfiguration.TlsCertificatePath is not null)
                {
                    _logger.LogWarning(message, nameof(_launcherConfiguration.TlsCertificatePath));
                }

                if (OperatingSystem.IsWindows() && !Runtime.IsAdministrator!.Value)
                {
                    _logger.LogWarning($"{nameof(_launcherConfiguration.UseHttpSysBinding)} is enabled but the fiskaltrust.Launcher was not started as an Administrator. Url binding may fail.");
                }

                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogWarning($"{nameof(_launcherConfiguration.UseHttpSysBinding)} is only supported on Windows.");
                }
            }

            // Create the appropriate host based on the hosting type
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

            // Use HTTP logging if the log level is set to Debug or lower
            if (_launcherConfiguration.LogLevel <= LogLevel.Debug)
            {
                app.UseHttpLogging();
            }

            await app.StartAsync();

            _logger.LogInformation("Started {hostingType} hosting on {url}", hostingType.ToString(), GetRestUri(uri));
            return app;
        }

        private WebApplication CreateRestHost<T>(WebApplicationBuilder builder, Uri uri, T instance, Action<WebApplication> addEndpoints)
        {
            // Configure JSON options
            builder.Services.Configure<JsonOptions>(options =>
            {
                options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
                options.SerializerOptions.Converters.Add(new NumberToStringConverter());
                options.SerializerOptions.Converters.Add(new CustomDateTimeConverter());
                options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                options.SerializerOptions.PropertyNamingPolicy = null;
                options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

            // Configure Kestrel server binding
            builder.WebHost.ConfigureBinding(uri, listenOptions => ConfigureTls(listenOptions), isHttps: !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificatePath) || !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificateBase64), allowSynchronousIO: true, useHttpSys: _launcherConfiguration.UseHttpSysBinding!.Value);

            var app = builder.Build();
            if (!OperatingSystem.IsWindows() || _launcherConfiguration.UseHttpSysBinding!.Value == false) { app.UsePathBase(uri.AbsolutePath); }

            app.UseRouting();
            addEndpoints(app);

            return app;
        }

        private string GetRestUri(Uri uri)
        {
            var isHttps = !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificatePath) || !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificateBase64);
            return uri.ToString().Replace("rest://", isHttps ? "https://" : "http://");
        }

        private WebApplication CreateSoapHost<T>(WebApplicationBuilder builder, Uri uri, T instance) where T : class
        {
            builder.WebHost.ConfigureBinding(uri, listenOptions => ConfigureTls(listenOptions), isHttps: !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificatePath) || !string.IsNullOrEmpty(_launcherConfiguration.TlsCertificateBase64), allowSynchronousIO: true, useHttpSys: _launcherConfiguration.UseHttpSysBinding!.Value);

            // Add WSDL support
            builder.Services.AddServiceModelServices().AddServiceModelMetadata();
            builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

            // Instance will automatically be pulled from the DI container
            builder.Services.AddSingleton(instance.GetType(), _ => instance);

            var app = builder.Build();
            if (!OperatingSystem.IsWindows() || _launcherConfiguration.UseHttpSysBinding!.Value == false) { app.UsePathBase(uri.AbsolutePath); }

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
                    case "net.pipe":
                        _logger.LogWarning("net.pipe url support will be added in an upcomming version of the launcher 2.0.");
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
            if (OperatingSystem.IsWindows() && _launcherConfiguration.UseHttpSysBinding!.Value)
            {
                _logger.LogWarning($"{nameof(_launcherConfiguration.UseHttpSysBinding)} is not supported for grpc.");
            }

            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(IAsyncResult), true);
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(AsyncCallback), true);
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(Stream), true);
            ProtoBuf.Meta.RuntimeTypeModel.Default.GetSchema(typeof(IPOS));
            ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof(IPOS), true).AddSubType(1, typeof(ifPOS.v1.IPOS));

            builder.WebHost.BindKestrel(uri, listenOptions => ConfigureTls(listenOptions), false, HttpProtocols.Http2);
            builder.Services.AddCodeFirstGrpc(options => options.EnableDetailedErrors = true);
            builder.Services.AddSingleton(instance);

            var app = builder.Build();
            if (!OperatingSystem.IsWindows() || _launcherConfiguration.UseHttpSysBinding!.Value == false)
            {
                app.UsePathBase(uri.AbsolutePath);
            }

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
    }

    public static class BindingExtensions
    {
        public static ConfigureWebHostBuilder ConfigureBinding(this ConfigureWebHostBuilder builder, Uri uri, Action<ListenOptions>? configureListeners = null, bool isHttps = false, bool? allowSynchronousIO = null, HttpProtocols? protocols = null, bool useHttpSys = false)
        {
            if (OperatingSystem.IsWindows())
            {
                if (useHttpSys)
                {
                    return builder.BindHttpSys(uri, isHttps, allowSynchronousIO);
                }
            }
            return builder.BindKestrel(uri, configureListeners, allowSynchronousIO, protocols);
        }

        public static ConfigureWebHostBuilder BindKestrel(this ConfigureWebHostBuilder builder, Uri uri, Action<ListenOptions>? configureListeners, bool? allowSynchronousIO, HttpProtocols? protocols)
        {
            void configureListenersInner(ListenOptions options)
            {
                if (protocols is not null)
                {
                    options.Protocols = protocols.Value;
                }

                configureListeners?.Invoke(options);
            }

            builder.ConfigureKestrel(options =>
            {
                if (allowSynchronousIO is not null)
                {
                    options.AllowSynchronousIO = allowSynchronousIO.Value;
                }

                options.ListenAnyIP(uri.Port, configureListenersInner);
            });

            return builder;
        }


        [SupportedOSPlatform("windows")]
        private static ConfigureWebHostBuilder BindHttpSys(this ConfigureWebHostBuilder builder, Uri uri, bool isHttps, bool? allowSynchronousIO)
        {
            builder.UseHttpSys(options =>
            {
                if (allowSynchronousIO is not null)
                {
                    options.AllowSynchronousIO = allowSynchronousIO.Value;
                }

                options.UrlPrefixes.Add(UrlPrefix.Create(isHttps ? "https" : "http", "*", uri.Port, uri.AbsolutePath));
            });

            return builder;
        }
    }
}
