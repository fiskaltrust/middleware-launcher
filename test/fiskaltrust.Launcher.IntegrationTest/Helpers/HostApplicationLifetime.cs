using fiskaltrust.Launcher.Extensions;
using Microsoft.Extensions.Hosting;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    public class TestLifetime : ILifetime
    {
        public IHostApplicationLifetime ApplicationLifetime { get => ApplicationLifetimeSource; init => throw new NotImplementedException(); }
        public HostApplicationLifetime ApplicationLifetimeSource { get; init; } = new HostApplicationLifetime();

        public void ServiceStartupCompleted()
        {
            ApplicationLifetimeSource.ApplicationStartedSource.Cancel();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ApplicationLifetimeSource.StopApplication();
            return Task.CompletedTask;
        }

        public async Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            var source = new TaskCompletionSource();

            ApplicationLifetimeSource.ApplicationStartedSource.Token.Register(() =>
            {
                source.TrySetResult();
            });

            ApplicationLifetimeSource.ApplicationStoppedSource.Token.Register(() =>
            {
                source.TrySetResult();
            });

            ApplicationLifetimeSource.ApplicationStoppingSource.Token.Register(() =>
            {
                source.TrySetResult();
            });

            await source.Task;
        }
    }

    public class HostApplicationLifetime : IHostApplicationLifetime
    {

        public CancellationTokenSource ApplicationStartedSource { get; set; }
        public CancellationTokenSource ApplicationStoppedSource { get; set; }
        public CancellationTokenSource ApplicationStoppingSource { get; set; }

        public HostApplicationLifetime()
        {
            ApplicationStartedSource = new CancellationTokenSource();
            ApplicationStoppedSource = new CancellationTokenSource();
            ApplicationStoppingSource = new CancellationTokenSource();
        }

        public HostApplicationLifetime(CancellationTokenSource applicationStarted, CancellationTokenSource applicationStopped, CancellationTokenSource applicationStopping)
        {
            ApplicationStartedSource = applicationStarted;
            ApplicationStoppedSource = applicationStopped;
            ApplicationStoppingSource = applicationStopping;
        }

        public CancellationToken ApplicationStarted { get => ApplicationStartedSource.Token; init => throw new NotImplementedException(); }

        public CancellationToken ApplicationStopped { get => ApplicationStoppedSource.Token; init => throw new NotImplementedException(); }

        public CancellationToken ApplicationStopping { get => ApplicationStoppingSource.Token; init => throw new NotImplementedException(); }

        public void StopApplication()
        {
            ApplicationStoppingSource.Cancel();
        }
    }
}