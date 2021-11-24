using System.Reflection;
using fiskaltrust.ifPOS.v1;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.AssemblyLoading;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.Services;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.storage.serialization.V0;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using Serilog;

namespace fiskaltrust.Launcher.ProcessHost
{

    public class ProcessHostPlebian : BackgroundService
    {
        private readonly PackageConfiguration _packageConfiguration;
        private readonly IProcessHostService? _processHostService;
        private readonly HostingService _hosting;
        private readonly PlebianConfiguration _plebianConfiguration;
        private readonly ILogger<ProcessHostPlebian> _logger;
        private readonly IServiceProvider _services;

        public ProcessHostPlebian(ILogger<ProcessHostPlebian> logger, HostingService hosting, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PlebianConfiguration plebianConfiguration, IServiceProvider services)
        {
            _logger = logger;
            _packageConfiguration = packageConfiguration;
            _hosting = hosting;
            _plebianConfiguration = plebianConfiguration;
            _services = services;

            if (launcherConfiguration.LauncherPort != null)
            {
                var channel = GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort!}");
                _processHostService = channel.CreateGrpcService<IProcessHostService>();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await StartHosting(_packageConfiguration.Url);

            await (_processHostService?.Started(_packageConfiguration.Id.ToString()) ?? Task.CompletedTask);

            var promise = new TaskCompletionSource();
            cancellationToken.Register(() =>
            {
                promise.SetResult();
            });

            if (_processHostService != null)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        Thread.Sleep(10000); // TODO make configurable?
                        try
                        {
                            await (_processHostService?.Ping() ?? Task.CompletedTask);
                        }
                        catch
                        {
                            Environment.Exit(0);
                        }
                    }
                }, cancellationToken);
            }

            await promise.Task;
        }

        private async Task StartHosting(string[] uris)
        {
            var hostingFailedCompletely = true;
            foreach (var uri in uris)
            {
                var url = new Uri(uri);

                (object instance, Type type) = _plebianConfiguration.PackageType switch
                {
                    PackageType.Queue => ((object)_services.GetRequiredService<IPOS>(), typeof(IPOS)),
                    PackageType.SCU => ((object)_services.GetRequiredService<IDESSCD>(), typeof(IDESSCD)),
                    _ => throw new NotImplementedException()
                };

                var hostingType = Enum.Parse<HostingType>(url.Scheme.ToUpper());

                Action<WebApplication>? addEndpoints = hostingType switch
                {
                    HostingType.REST => _plebianConfiguration.PackageType switch
                    {
                        PackageType.Queue => (WebApplication app) => app.AddQueueEndpoints((IPOS)instance),
                        PackageType.SCU => (WebApplication app) => app.AddScuEndpoints((IDESSCD)instance),
                        _ => throw new NotImplementedException()
                    },
                    _ => null
                };
                try
                {
                    await _hosting.HostService(type, url, hostingType, instance, addEndpoints);
                    hostingFailedCompletely = false;
                }
                catch (Exception e)
                {
                    _logger.LogError("Could not start {url} hosting. {Message}, {HelpLink}, {InnerException}", url, e.Message, e.HelpLink ?? "", e.InnerException);
                }
            }

            if (hostingFailedCompletely)
            {
                throw new Exception("No host could be started.");
            }
        }
    }
}
