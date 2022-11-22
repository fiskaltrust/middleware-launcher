using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
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

    public class ProcessHostPlebian : BackgroundService
    {
        private readonly PackageConfiguration _packageConfiguration;
        private readonly IProcessHostService? _processHostService;
        private readonly HostingService _hosting;
        private readonly PlebianConfiguration _plebianConfiguration;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ILogger<ProcessHostPlebian> _logger;
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;

        public ProcessHostPlebian(ILogger<ProcessHostPlebian> logger, HostingService hosting, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PlebianConfiguration plebianConfiguration, IServiceProvider services, IHostApplicationLifetime lifetime, IProcessHostService? processHostService = null)
        {
            _logger = logger;
            _hosting = hosting;
            _launcherConfiguration = launcherConfiguration;
            _packageConfiguration = packageConfiguration;
            _plebianConfiguration = plebianConfiguration;
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
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error Starting Hosting");
                _lifetime.StopApplication();
                return;
            }

            await (_processHostService?.Started(_packageConfiguration.Id.ToString()) ?? Task.CompletedTask);

            var promise = new TaskCompletionSource();
            cancellationToken.Register(() =>
            {
                _logger.LogInformation("Stopping Package");

                try
                {

                    if (_plebianConfiguration.PackageType == PackageType.Helper)
                    {
                        var helper = _services.GetRequiredService<IHelper>();
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
                            await (_processHostService?.Ping() ?? Task.CompletedTask);
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

            object instance = _plebianConfiguration.PackageType switch
            {
                PackageType.Queue => _services.GetRequiredService<IPOS>(),
                PackageType.SCU => _services.GetRequiredService<IDESSCD>(),
                PackageType.Helper => _services.GetRequiredService<IHelper>(),
                _ => throw new NotImplementedException()
            };

            foreach (var uri in uris)
            {
                var url = new Uri(uri);
                var hostingType = GetHostingType(url);

                Action<WebApplication, object>? addEndpoints = hostingType switch
                {
                    HostingType.REST => _plebianConfiguration.PackageType switch
                    {
                        PackageType.Queue => (app, instance) => app.AddQueueEndpoints((IPOS)instance),
                        PackageType.SCU => (app, instance) => app.AddScuEndpoints((IDESSCD)instance),
                        PackageType.Helper => (_, _) => { }
                        ,
                        _ => throw new NotImplementedException()
                    },
                    _ => null
                };

                try
                {
                    switch (_plebianConfiguration.PackageType)
                    {
                        case PackageType.SCU:
                            await _hosting.HostService<IDESSCD>(url, hostingType, (IDESSCD)instance, addEndpoints);
                            break;
                        case PackageType.Queue:
                            await _hosting.HostService<IPOS>(url, hostingType, (IPOS)instance, addEndpoints);
                            break;
                        case PackageType.Helper:
                            await _hosting.HostService<IHelper>(url, hostingType, (IHelper)instance, addEndpoints);
                            ((IHelper)instance).StartBegin();
                            ((IHelper)instance).StartEnd();
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

        private static HostingType GetHostingType(Uri url)
        {
            return url.Scheme.ToLowerInvariant() switch
            {
                "grpc" => HostingType.GRPC,
                "rest" => HostingType.REST,
                "http" or "https" or "net.tcp" => HostingType.SOAP,
                _ => throw new NotImplementedException($"The hosting type for the URL {url} is currently not supported.")
            };
        }
    }
}
