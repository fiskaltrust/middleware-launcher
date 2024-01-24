using System.Runtime.Versioning;
using System.Text;
using fiskaltrust.Launcher.Helpers;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace fiskaltrust.Launcher.Extensions
{
    public record ServiceType(ServiceTypes Type);

    public enum ServiceTypes
    {
        WindowsService,
        SystemdService,
        ConsoleApplication
    }

    static class LifetimeExtensions
    {
        public static IHostBuilder UseCustomHostLifetime(this IHostBuilder builder, string[] args)
        {
            if (WindowsServiceHelpers.IsWindowsService())
            {
                builder.UseWindowsService();

                return builder.ConfigureServices(services =>
                {
                    services.AddSingleton(new ServiceType(ServiceTypes.WindowsService));
                    var lifetime = services.FirstOrDefault(s => s.ImplementationType == typeof(WindowsServiceLifetime));

                    if (lifetime != null)
                    {
                        services.Remove(lifetime);
                    }

#pragma warning disable CA1416
                    services.AddSingleton<ILifetime, CustomWindowsServiceLifetime>();
                    services.AddSingleton<IHostLifetime>(sp => sp.GetRequiredService<ILifetime>());
#pragma warning restore CA1416
                });
            }
            else if (CustomSystemdHelper.IsSystemdService(args))
            {
                return builder.ConfigureServices(services =>
                {
                    services.AddSingleton(new ServiceType(ServiceTypes.SystemdService));
                    services.AddSingleton<ISystemdNotifier, SystemdNotifier>();
                    
#pragma warning disable CA1416
                    services.AddSingleton<ILifetime, CustomSystemDServiceLifetime>();
                    services.AddSingleton<IHostLifetime>(sp => sp.GetRequiredService<ILifetime>());
#pragma warning restore CA1416
                });
            }
            else
            {
                Console.OutputEncoding = Encoding.UTF8;
                builder.ConfigureServices(services => services
                    .AddSingleton<ILifetime, Lifetime>()
                    .AddSingleton(new ServiceType(ServiceTypes.ConsoleApplication)));

                builder.UseConsoleLifetime();
                return builder;
            }
        }
    }

    public interface ILifetime : IHostLifetime
    {
        public IHostApplicationLifetime ApplicationLifetime { get; init; }

        public void ServiceStartupCompleted();
    }


    public class Lifetime : ILifetime
    {
        private readonly TaskCompletionSource _started = new();

        public IHostApplicationLifetime ApplicationLifetime { get; init; }

        public Lifetime(IHostApplicationLifetime applicationLifetime)
        {
            ApplicationLifetime = applicationLifetime;
        }

        public void ServiceStartupCompleted()
        {
            ApplicationLifetime.ApplicationStarted.Register(() => _started.TrySetResult());
        }

        public async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _started.TrySetResult());

            await _started.Task;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ApplicationLifetime.StopApplication();
            return Task.CompletedTask;
        }
    }

    [SupportedOSPlatform("windows")]
    public class CustomWindowsServiceLifetime : WindowsServiceLifetime, ILifetime
    {
        private readonly CancellationTokenSource _starting = new();
        private readonly ManualResetEventSlim _started = new();

        public IHostApplicationLifetime ApplicationLifetime { get; init; }

        public CustomWindowsServiceLifetime(
            IHostEnvironment environment,
            IHostApplicationLifetime applicationLifetime,
            ILoggerFactory loggerFactory,
            IOptions<HostOptions> optionsAccessor)
            : base(environment, applicationLifetime, loggerFactory, optionsAccessor)
        {
            ApplicationLifetime = applicationLifetime;
        }

        public void ServiceStartupCompleted()
        {
            ApplicationLifetime.ApplicationStarted.Register(() => _started.Set());
        }

        public new async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_starting.Token, cancellationToken);
                await base.WaitForStartAsync(cts.Token);
            }
            catch (OperationCanceledException) when (_starting.IsCancellationRequested) { }
        }

        protected override void OnStart(string[] args)
        {
            _starting.Cancel();

            _started.Wait(ApplicationLifetime.ApplicationStopping);
            if (!ApplicationLifetime.ApplicationStarted.IsCancellationRequested)
            {
                throw new Exception("Failed to start host");
            }

            base.OnStart(args);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _starting.Dispose();
                _started.Set();
            }

            base.Dispose(disposing);
        }
    }
    [SupportedOSPlatform("linux")]
    public class CustomSystemDServiceLifetime : SystemdLifetime, ILifetime
    {
        private readonly CancellationTokenSource _starting = new();
        private readonly ManualResetEventSlim _started = new();

        public IHostApplicationLifetime ApplicationLifetime { get; init; }
        private readonly ISystemdNotifier _systemdNotifier;

        public CustomSystemDServiceLifetime(
            IHostEnvironment environment,
            IHostApplicationLifetime applicationLifetime,
            ILoggerFactory loggerFactory,
            ISystemdNotifier systemdNotifier)
            : base(environment, applicationLifetime, systemdNotifier, loggerFactory)
        {
            ApplicationLifetime = applicationLifetime;
            _systemdNotifier = systemdNotifier;
        }

        public void ServiceStartupCompleted()
        {
            _systemdNotifier.Notify(ServiceState.Ready);
            ApplicationLifetime.ApplicationStarted.Register(_started.Set);
        }

        public new async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_starting.Token, cancellationToken);
                await base.WaitForStartAsync(cts.Token);
            }
            catch (OperationCanceledException) when (_starting.IsCancellationRequested) { }
        }
    }
}