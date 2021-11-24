
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
            var packageConfiguration = services.GetService<PackageConfiguration>();

            var consoleOutputTemplate = packageConfiguration switch {
                null => $"[{{Timestamp:HH:mm:ss}} {{Level:u3}}] {{Message:lj}}{{NewLine}}{{Exception}}",
                _ => $"[{{Timestamp:HH:mm:ss}} {{Level:u3}} {packageConfiguration.Package}] {{Message:lj}}{{NewLine}}{{Exception}}",
            };

            return loggerConfiguration.MinimumLevel.Is(Serilog.Extensions.Logging.LevelConvert.ToSerilogLevel(launcherConfiguration.LogLevel!.Value))
            .WriteTo.Console(outputTemplate: consoleOutputTemplate)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Grpc", LogEventLevel.Warning)
            .MinimumLevel.Override("ProtoBuf", LogEventLevel.Warning)
            .WriteTo.File(Path.Join(launcherConfiguration.LogFolder, $"log{suffix}-.txt"), rollingInterval: RollingInterval.Day, shared: true);
        }

    }
}
