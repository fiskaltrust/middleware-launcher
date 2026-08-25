using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.Extensions
{
    static class LauncherConfigurationExtension
    {
        public static void LogConfigurationWarnings(this LauncherConfiguration launcherConfiguration, ILogger logger)
        {
            if (launcherConfiguration.Raw(l => l.UseLegacyDataProtection.HasValue))
            {
                logger.ConfigurationDeprecated("UseLegacyDataProtection");
            }
        }
    }
}
