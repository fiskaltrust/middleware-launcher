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
        private readonly IMiddlewareBootstrapper _bootstrapper;
        private readonly ServiceCollection _services;
        private readonly HostingService _hosting;
        private readonly PlebianConfiguration _plebianConfiguration;
        private readonly ILogger<ProcessHostPlebian> _logger;

        public ProcessHostPlebian(ILogger<ProcessHostPlebian> logger, HostingService hosting, LauncherConfiguration launcherConfiguration, PackageConfiguration packageConfiguration, PlebianConfiguration plebianConfiguration)
        {
            _logger = logger;
            _packageConfiguration = packageConfiguration;
            _hosting = hosting;
            _plebianConfiguration = plebianConfiguration;

            if (launcherConfiguration.LauncherPort != null && launcherConfiguration.LauncherPort != 0)
            {
                var channel = GrpcChannel.ForAddress($"http://localhost:{launcherConfiguration.LauncherPort}");
                _processHostService = channel.CreateGrpcService<IProcessHostService>();
            }

            _services = new ServiceCollection();
            _services.AddLogging(builder =>
            {
                var logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File($"log-{packageConfiguration.Id}.txt", rollingInterval: RollingInterval.Day, shared: true)
                    .CreateLogger();
                builder.AddSerilog(logger, dispose: true);
            });


            _bootstrapper = PluginLoader.LoadPlugin<IMiddlewareBootstrapper>(launcherConfiguration.ServiceFolder!, packageConfiguration.Package);
            _bootstrapper.Id = packageConfiguration.Id;
            _bootstrapper.Configuration = packageConfiguration.Configuration.ToDictionary(x => x.Key, x => (object?)x.Value.ToString());
            _bootstrapper.ConfigureServices(_services);
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

            if(_processHostService != null) {
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

                (object instance, Type type) = _plebianConfiguration.PackageType switch {
                    PackageType.Queue => ((object)_services.BuildServiceProvider().GetRequiredService<IPOS>(), typeof(IPOS)),
                    PackageType.SCU => ((object)_services.BuildServiceProvider().GetRequiredService<IDESSCD>(), typeof(IDESSCD)),
                    _ => throw new NotImplementedException()
                };

                var hostingType = Enum.Parse<HostingType>(url.Scheme.ToUpper());
                
                Action<WebApplication>? addEndpoints = hostingType switch {
                    HostingType.REST => _plebianConfiguration.PackageType switch {
                        PackageType.Queue => (WebApplication app) => app.AddQueueEndpoints((IPOS)instance),
                        PackageType.SCU => (WebApplication app) => app.AddScuEndpoints((IDESSCD)instance),
                        _ => throw new NotImplementedException()
                    },
                    _ => null
                };
                try {
                    await _hosting.HostService(type, url, hostingType, instance, addEndpoints);
                    hostingFailedCompletely = false;
                } catch(Exception e) {
                    _logger.LogError("Could not start {} hosting. {}, {}", url, e.Message, e.HelpLink);
                }
            }

            if(hostingFailedCompletely) {
                throw new Exception("No host could be started.");
            }
        }
    }
}
