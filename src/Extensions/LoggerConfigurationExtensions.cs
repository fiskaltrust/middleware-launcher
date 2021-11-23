
using Serilog;
using Serilog.Events;

namespace fiskaltrust.Launcher.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, string? suffix = null)
        {
            if (suffix != null)
            {
                suffix = $"-{suffix}";
            }
            else
            {
                suffix = "";
            }
            return loggerConfiguration.MinimumLevel.Debug()
            .WriteTo.Console()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .WriteTo.File($"log{suffix}-.txt", rollingInterval: RollingInterval.Day, shared: true);
        }

    }
}
