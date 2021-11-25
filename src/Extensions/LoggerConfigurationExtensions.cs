
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using Serilog.Events;

namespace fiskaltrust.Launcher.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, IServiceProvider services, string? suffix = null)
        {
            if (suffix != null)
            {
                suffix = $"-{suffix}";
            }
            else
            {
                suffix = "";
            }
            var launcherConfiguration = services.GetRequiredService<LauncherConfiguration>();

            return loggerConfiguration.MinimumLevel.Is(Serilog.Extensions.Logging.LevelConvert.ToSerilogLevel(launcherConfiguration.LogLevel!.Value))
            .WriteTo.Console()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .MinimumLevel.Override("ProtoBuf", LogEventLevel.Warning)
            .WriteTo.File(Path.Join(launcherConfiguration.LogFolder, $"log{suffix}-.txt"), rollingInterval: RollingInterval.Day, shared: true);
        }
    }
}
