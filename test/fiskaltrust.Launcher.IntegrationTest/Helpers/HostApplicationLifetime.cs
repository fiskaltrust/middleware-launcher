using Microsoft.Extensions.Hosting;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
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

        public CancellationToken ApplicationStarted { get; init; }

        public CancellationToken ApplicationStopped { get; init; }

        public CancellationToken ApplicationStopping { get; init; }

        public void StopApplication()
        {
            ApplicationStoppingSource.Cancel();
        }
    }
}