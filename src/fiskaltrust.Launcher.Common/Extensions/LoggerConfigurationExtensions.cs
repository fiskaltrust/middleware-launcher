using fiskaltrust.Launcher.Common.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace fiskaltrust.Launcher.Common.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, LauncherConfiguration? launcherConfiguration = null, string[]? suffix = null, bool aspLogging = false)
        {
            if (launcherConfiguration is not null)
            {
                loggerConfiguration = loggerConfiguration.MinimumLevel.Is(Serilog.Extensions.Logging.LevelConvert.ToSerilogLevel(launcherConfiguration.LogLevel!.Value));
            }

            loggerConfiguration = loggerConfiguration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}{EnrichedPackage}{EnrichedId}{EnrichedContext}] {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: LogEventLevel.Error
            )
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", aspLogging ? LogEventLevel.Information : LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .MinimumLevel.Override("ProtoBuf", LogEventLevel.Warning);

            if (launcherConfiguration is not null)
            {

                loggerConfiguration = loggerConfiguration.WriteTo.Logger(configuration =>
                    configuration
                        .Filter.ByExcluding(Matching.WithProperty("EnrichedId"))
                        .WriteTo.File(
                            Path.Join(launcherConfiguration.LogFolder, $"log{(suffix is null ? null : "_" + string.Join("_", suffix))}_.txt"),
                            rollingInterval: RollingInterval.Day,
                            shared: true,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}{EnrichedContext}] {Message:lj}{NewLine}{Exception}"));
            }

            return loggerConfiguration;
        }
    }
}
