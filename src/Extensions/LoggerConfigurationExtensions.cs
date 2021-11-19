
using Serilog;

namespace fiskaltrust.Launcher.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, string? suffix = null)
        {
            if(suffix != null) {
                suffix = $"-{suffix}";
            } else {
                suffix = "";
            }
            return loggerConfiguration.MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"log{suffix}-.txt", rollingInterval: RollingInterval.Day, shared: true);
        }

    }
}
