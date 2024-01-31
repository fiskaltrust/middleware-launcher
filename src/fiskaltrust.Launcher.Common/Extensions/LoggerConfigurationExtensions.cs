using fiskaltrust.Launcher.Common.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace fiskaltrust.Launcher.Common.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        private static string OutputTemplate(string timestamp) => $"[{{{timestamp}}} {{Level:u3}}{{EnrichedPackage}}{{EnrichedContext}}] {{Message:lj}}{{NewLine}}{{Exception}}";

        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, LauncherConfiguration? launcherConfiguration = null, bool aspLogging = false)
        {
            if (launcherConfiguration is not null)
            {
                loggerConfiguration = loggerConfiguration.MinimumLevel.Is(Serilog.Extensions.Logging.LevelConvert.ToSerilogLevel(launcherConfiguration.LogLevel!.Value));
            }

            loggerConfiguration = loggerConfiguration
                .WriteTo.Console(
                    outputTemplate: OutputTemplate("Timestamp:HH:mm:ss"),
                    standardErrorFromLevel: LogEventLevel.Error
                )
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", aspLogging ? LogEventLevel.Information : LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
                .MinimumLevel.Override("ProtoBuf", LogEventLevel.Error);

            return loggerConfiguration;
        }

        public static LoggerConfiguration AddFileLoggingConfiguration(this LoggerConfiguration loggerConfiguration, LauncherConfiguration launcherConfiguration, string?[] logFileSuffix)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Logger(configuration =>
                configuration
                    .WriteTo.File(
                        Path.Join(launcherConfiguration.LogFolder, $"log_{string.Join("_", logFileSuffix.Where(s => s is not null))}_.txt"),
                        rollingInterval: RollingInterval.Day,
                        shared: true,
                        outputTemplate: OutputTemplate("Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz")
                        ));

            return loggerConfiguration;
        }
    }
}
