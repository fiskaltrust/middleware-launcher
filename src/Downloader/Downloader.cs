
using System.IO.Compression;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Download
{
    public class Downloader
    {
        private readonly LauncherConfiguration _configuration;
        private readonly ILogger<Downloader> _logger;

        public Downloader(ILogger<Downloader> logger, LauncherConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public string GetPackagePath(PackageConfiguration configuration)
        {
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");

            if (File.Exists(targetName))
            {
                return targetName;
            } else {
              throw new Exception("Could not find Package.");
            }
        }

        public async Task DownloadPackage(PackageConfiguration configuration)
        {
            var name = $"{configuration.Package}-{configuration.Version}";
            var targetPath = Path.Combine(_configuration.ServiceFolder!, "service", _configuration.CashboxId?.ToString()!, configuration.Id.ToString());
            var targetName = Path.Combine(targetPath, $"{configuration.Package}.dll");

            if (File.Exists(targetName))
            {
                return;
            }

            if (Directory.Exists(targetPath)) { Directory.Delete(targetPath, true); }

            Directory.CreateDirectory(targetPath);

            var sourcePath = Path.Combine(_configuration.ServiceFolder!, "cache", "packages", $"{name}.zip");

            for (var i = 0; i <= 1; i++)
            {
                if (!File.Exists(sourcePath))
                {
                    _logger.LogInformation("Downloading Package.");
                    // TODO Download Package
                }
                else
                {
                    _logger.LogDebug("Found Package in cache.");
                }

                ZipFile.ExtractToDirectory(sourcePath, targetPath);

                if (!File.Exists(targetName))
                {
                    if (i == 0) { File.Delete(sourcePath); }
                    continue;
                }

                return;
            }

            throw new Exception("Downloaded Package is invalid");
        }

        public static async Task<string> DownloadConfiguration(LauncherConfiguration configuration, string? cashboxConfigurationFile) {
            var cashboxConfigurationPath = cashboxConfigurationFile ?? Path.Join(configuration.ServiceFolder!, "service", configuration.CashboxId!.ToString(), "configuration.json");

            // TODO Download newest configuration

            return cashboxConfigurationPath;
        }
    }
}
