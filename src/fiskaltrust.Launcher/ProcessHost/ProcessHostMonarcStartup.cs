using System.Security.Policy;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Download;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using fiskaltrust.storage.serialization.V0;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace fiskaltrust.Launcher.ProcessHost
{
    public record ProcessHostServicePort(int Value);

    public class ProcessHostMonarcStartup : BackgroundService
    {
        public class AlreadyLoggedException : Exception { }

        private readonly Dictionary<Guid, IProcessHostMonarch> _hosts;
        private readonly LauncherConfiguration _launcherConfiguration;
        private readonly ftCashBoxConfiguration _cashBoxConfiguration;
        private readonly PackageDownloader _downloader;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILifetime _lifetime;
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public ProcessHostMonarcStartup(ILoggerFactory loggerFactory, ILogger<ProcessHostMonarcStartup> logger, Dictionary<Guid, IProcessHostMonarch> hosts, LauncherConfiguration launcherConfiguration, ftCashBoxConfiguration cashBoxConfiguration, PackageDownloader downloader, ILifetime lifetime, LauncherExecutablePath launcherExecutablePath, IHostApplicationLifetime hostApplicationLifetime, IServer server)
        {
            _loggerFactory = loggerFactory;
            _logger = logger;
            _hosts = hosts;
            _launcherConfiguration = launcherConfiguration;
            _cashBoxConfiguration = cashBoxConfiguration;
            _downloader = downloader;
            _lifetime = lifetime;
            _launcherExecutablePath = launcherExecutablePath;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _lifetime.ApplicationLifetime.ApplicationStopping.Register(() => _logger.LogInformation("Shutting down launcher."));

            StartupLogging();

            _downloader.CopyPackagesToCache();

            try
            {
                foreach (var scu in _cashBoxConfiguration.ftSignaturCreationDevices)
                {
                    await StartProcessHostMonarch(scu, PackageType.SCU, cancellationToken);
                }

                foreach (var queue in _cashBoxConfiguration.ftQueues)
                {
                    await StartProcessHostMonarch(queue, PackageType.Queue, cancellationToken);
                }

                foreach (var helper in _cashBoxConfiguration.helpers)
                {
                    await StartProcessHostMonarch(helper, PackageType.Helper, cancellationToken);
                }
            }
            catch (Exception e)
            {
                if (e is not AlreadyLoggedException) { _logger.LogError(e, "Error Starting Monarchs"); }
                _lifetime.ApplicationLifetime.StopApplication();
                return;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Started all packages.");
                if (!WindowsServiceHelpers.IsWindowsService())
                {
                    _logger.LogInformation("Press CTRL+C to exit.");
                }
                _lifetime.ServiceStartupCompleted();
            }

            if (_hosts.Count == 0)
            {
                // Wait for shutdown of the launcher (ctrl+c or windows service stop)
                // if we dont have this and the ProcessHostMonarcStartup BackgroundService finished the Launcher shuts down with a TaskCancelledException
                await new TaskCompletionSource().Task;
            }

            foreach (var host in _hosts)
            {
                host.Value.SetStartupCompleted();
            }

            try
            {
                await Task.WhenAll(_hosts.Select(h => h.Value.GetStopped()));
            }
            catch
            {
                foreach (var failed in _hosts.Where(h => !h.Value.GetStopped().IsCompletedSuccessfully).Select(h => (h.Key, h.Value.GetStopped().Exception)))
                {
                    _logger.LogWarning(failed.Exception, "ProcessHost {Id} had crashed.", failed.Key);
                }
            }
            _lifetime.ApplicationLifetime.StopApplication();
        }

        private async Task StartProcessHostMonarch(PackageConfiguration configuration, PackageType packageType, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _downloader.DownloadPackageAsync(configuration);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not download package.");
                throw new AlreadyLoggedException();
            }

            var monarch = new ProcessHostMonarch(
                _loggerFactory.CreateLogger<ProcessHostMonarch>(),
                _launcherConfiguration,
                configuration,
                packageType,
                _launcherExecutablePath);

            if (!cancellationToken.IsCancellationRequested)
            {
                _hosts.Add(
                    configuration.Id,
                    monarch
                );
            }

            try
            {
                await monarch.Start(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Started {Package} {Id}.", configuration.Package, configuration.Id);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Could not start {Package} {Id}.", configuration.Package, configuration.Id);
                // not throwing here keeps the launcher alive even when theres a package completely failed
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not start {Package} {Id}.", configuration.Package, configuration.Id);
                throw new AlreadyLoggedException();
            }
        }

        private void StartupLogging()
        {
            _logger.LogInformation("fiskaltrust.Launcher: {version}", Common.Constants.Version.CurrentVersion);
            _logger.LogInformation("OS:                   {OS}, {Bit}", Environment.OSVersion.VersionString, Environment.Is64BitOperatingSystem ? "64Bit" : "32Bit");
            if (OperatingSystem.IsWindows())
            {
                _logger.LogInformation("Admin User:           {admin}", Runtime.IsAdministrator!);
            }
            _logger.LogInformation("CWD:                  {CWD}", Path.GetFullPath("./"));
            _logger.LogInformation("CashBoxId:            {CashBoxId}", _launcherConfiguration.CashboxId);
        }
    }
}
