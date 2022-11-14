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

            (object instance, Type type) = _plebianConfiguration.PackageType switch
            {
                PackageType.Queue => ((object)_services.GetRequiredService<IPOS>(), typeof(IPOS)),
                PackageType.SCU => (_services.GetRequiredService<IDESSCD>(), typeof(IDESSCD)),
                PackageType.Helper => (_services.GetRequiredService<IHelper>(), typeof(IHelper)),
                _ => throw new NotImplementedException()
            };

            foreach (var uri in uris)
            {
                var url = new Uri(uri);

                var hostingType = Enum.Parse<HostingType>(url.Scheme.ToUpper());

                Action<WebApplication>? addEndpoints = hostingType switch
                {
                    HostingType.REST => _plebianConfiguration.PackageType switch
                    {
                        PackageType.Queue => app => app.AddQueueEndpoints((IPOS)instance),
                        PackageType.SCU => app => app.AddScuEndpoints((IDESSCD)instance),
                        PackageType.Helper => _ => { }
                        ,
                        _ => throw new NotImplementedException()
                    },
                    _ => null
                };

                try
                {
                    await _hosting.HostService(type, url, hostingType, instance, addEndpoints);
                    if (_plebianConfiguration.PackageType == PackageType.Helper)
                    {
                        ((IHelper)instance).StartBegin();
                        ((IHelper)instance).StartEnd();
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
    }
}
