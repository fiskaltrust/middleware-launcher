using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.Extensions
{
    static class LauncherConfigurationExtension
    {
        public static void LogConfigurationWarnings(this LauncherConfiguration launcherConfiguration, Serilog.ILogger logger)
        {
            if (launcherConfiguration.UseLegacyDataProtection.HasValue)
            {
                logger.Warning("Configuration 'UseLegacyDataProtection' is depreciated and will be removed in future.");
            }
        }
    }
}
