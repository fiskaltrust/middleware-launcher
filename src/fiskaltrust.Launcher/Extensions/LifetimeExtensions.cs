using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;

namespace fiskaltrust.Launcher.Extensions
{
    static class LifetimeExtensions
    {
        public static IHostBuilder UseCustomHostLifetime(this IHostBuilder builder)
        {
            if (WindowsServiceHelpers.IsWindowsService())
            {
                builder.UseWindowsService();

                return builder.ConfigureServices(services =>
                {
                    var lifetime = services.FirstOrDefault(s => s.ImplementationType == typeof(WindowsServiceLifetime));

                    if (lifetime != null)
                    {
                        services.Remove(lifetime);
                    }

                    services.AddSingleton<Lifetime>();
                    services.AddSingleton<IHostLifetime>(sp => sp.GetRequiredService<Lifetime>());
                });
            }
            else
            {
                builder.ConfigureServices(services => services.AddSingleton<Lifetime>());
                builder.UseConsoleLifetime();
                return builder;
            }
        }
    }

    public class Lifetime : WindowsServiceLifetime, IHostLifetime
    {
        private readonly CancellationTokenSource _starting = new();
        private readonly ManualResetEventSlim _started = new();
        public IHostApplicationLifetime ApplicationLifetime { get; init; }

        public Lifetime(
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
}