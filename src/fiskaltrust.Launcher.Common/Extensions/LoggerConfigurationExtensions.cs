using fiskaltrust.Launcher.Common.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace fiskaltrust.Launcher.Common.Extensions
{
    public static class LoggerConfigurationExtensions
    {
        public static LoggerConfiguration AddLoggingConfiguration(this LoggerConfiguration loggerConfiguration, LauncherConfiguration launcherConfiguration, string[]? suffix = null, bool aspLogging = false)
        {
            return loggerConfiguration.MinimumLevel.Is(Serilog.Extensions.Logging.LevelConvert.ToSerilogLevel(launcherConfiguration.LogLevel!.Value))
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}{EnrichedPackage}{EnrichedId}{EnrichedContext}] {Message:lj}{NewLine}{Exception}")
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware", aspLogging ? LogEventLevel.Information : LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .MinimumLevel.Override("ProtoBuf", LogEventLevel.Warning)
            .WriteTo.Logger(configuration =>
                configuration
                    .Filter.ByExcluding(Matching.WithProperty("EnrichedId"))
                    .WriteTo.File(
                        Path.Join(launcherConfiguration.LogFolder, $"log{(suffix is null ? null : "_" + string.Join("_", suffix))}_.txt"),
                        rollingInterval: RollingInterval.Day,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}{EnrichedContext}] {Message:lj}{NewLine}{Exception}"));
        }

    }
}