using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.ifPOS.v1.it;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Services;
using fiskaltrust.Launcher.Services.Interfaces;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.ProcessHost
{

    public class ProcessHostPlebeian : BackgroundService
    {
        private readonly PackageConfiguration _packageConfiguration;
        private readonly IProcessHostService? _processHostService;
        private readonly HostingService _hosting;
        private readonly PlebeianConfiguration _plebeianConfiguration;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ILogger<ProcessHostPlebeian> _logger;
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;

        public ProcessHostPlebeian(ILogger<ProcessHostPlebeian> logger, HostingService hosting, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PlebeianConfiguration plebeianConfiguration, IServiceProvider services, IHostApplicationLifetime lifetime, IProcessHostService? processHostService = null)
        {
            _logger = logger;
            _hosting = hosting;
            _launcherConfiguration = launcherConfiguration;
            _packageConfiguration = packageConfiguration;
            _plebeianConfiguration = plebeianConfiguration;
            _services = services;
            _processHostService = processHostService;
            _lifetime = lifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Package: {Package} {Version}", _packageConfiguration.Package, _packageConfiguration.Version);
            _logger.LogInformation("Id:      {Id}", _packageConfiguration.Id);

            try
            {
                await StartHosting(_packageConfiguration.Url);

                if (_plebeianConfiguration.PackageType == PackageType.Helper)
                {
                    _logger.LogDebug("Helper StartBegin() and StartEnd()");
                    var helper = _services.GetRequiredService<IHelper>();
                    helper.StartBegin();
                    helper.StartEnd();
                }
            }
            catch
            {
                _logger.LogError("Error Starting Hosting");
                throw;
            }

            _processHostService?.Started(_packageConfiguration.Id.ToString());

            var promise = new TaskCompletionSource();
            cancellationToken.Register(() =>
            {
                _logger.LogInformation("Stopping Package");

                try
                {

                    if (_plebeianConfiguration.PackageType == PackageType.Helper)
                    {
                        IHelper helper;
                        try
                        {
                            helper = _services.GetRequiredService<IHelper>();
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                        helper.StopBegin();
                        helper.StopEnd();
                    };
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception when calling StopBegin and StopEnd hooks");
                }

                promise.SetResult();
            });

            if (_processHostService is not null)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(_launcherConfiguration.ProcessHostPingPeriodSec!.Value * 1000);
                        try
                        {
                            _processHostService?.Ping();
                        }
                        catch
                        {
                            _lifetime.StopApplication();
                        }
                    }
                }, cancellationToken);
            }

            await promise.Task;
        }

        private async Task StartHosting(string[] uris)
        {
            var hostingFailedCompletely = uris.Length > 0;

            (object instance, Action<WebApplication> addEndpoints, Type instanceInterface) = _plebeianConfiguration.PackageType switch
            {
                PackageType.Queue => GetQueue(_services),
                PackageType.SCU => GetScu(_services),
                PackageType.Helper => (_services.GetRequiredService<IHelper>(), (WebApplication _) => { }, typeof(IHelper)),
                _ => throw new NotImplementedException()
            };


            foreach (var uri in uris)
            {
                var url = new Uri(uri);
                var hostingType = GetHostingType(url);
                if (hostingType is null)
                {
                    continue;
                }

                Action<WebApplication>? addEndpointsInner = hostingType switch
                {
                    HostingType.REST => addEndpoints,
                    _ => null
                };

                try
                {
                    switch (_plebeianConfiguration.PackageType)
                    {
                        case PackageType.SCU:
                            if (instanceInterface == typeof(IDESSCD))
                            {
                                await _hosting.HostService(url, hostingType.Value, (IDESSCD)instance, addEndpoints);
                            }
                            else if (instanceInterface == typeof(IITSSCD))
                            {
                                await _hosting.HostService(url, hostingType.Value, (IITSSCD)instance, addEndpoints);
                            }
                            break;
                        case PackageType.Queue:
                            await _hosting.HostService(url, hostingType.Value, (IPOS)instance, addEndpoints);
                            break;
                        case PackageType.Helper:
                            await _hosting.HostService(url, hostingType.Value, (IHelper)instance, addEndpoints);
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    hostingFailedCompletely = false;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not start {url} hosting.", url);
                }
            }

            if (hostingFailedCompletely)
            {
                throw new Exception("No host could be started.");
            }
        }

        private static (object, Action<WebApplication>, Type) GetQueue(IServiceProvider services)
        {
            var queue = services.GetRequiredService<IPOS>();

            return (queue, (WebApplication app) => app.AddQueueEndpoints(queue), typeof(IPOS));
        }

        private static (object, Action<WebApplication>, Type) GetScu(IServiceProvider services)
        {
            var scuDe = services.GetService<IDESSCD>();

            if (scuDe is not null)
            {
                return (scuDe, (WebApplication app) => app.AddScuDeEndpoints(scuDe), typeof(IDESSCD));
            }

            var scuIt = services.GetService<IITSSCD>();
            if (scuIt is not null)
            {
                return (scuIt, (WebApplication app) => app.AddScuItEndpoints(scuIt), typeof(IITSSCD));
            }

            throw new Exception("Could not resolve SCU with supported country. (Curently supported are DE and IT)");
        }

        private static HostingType? GetHostingType(Uri url)
        {
            return url.Scheme.ToLowerInvariant() switch
            {
                "grpc" => HostingType.GRPC,
                "rest" => HostingType.REST,
                "http" or "https" or "net.tcp" => HostingType.SOAP,
                "net.pipe" => null,
                _ => throw new NotImplementedException($"The hosting type for the URL {url} is currently not supported.")
            };
        }
    }
}
