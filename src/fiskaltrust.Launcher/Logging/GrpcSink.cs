
using fiskaltrust.Launcher.Services.Interfaces;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace fiskaltrust.Launcher.Logging
{
    public static class GrpcSinkExtensions
    {
        public static LoggerConfiguration GrpcSink(this LoggerSinkConfiguration loggerConfiguration, PackageConfiguration packageConfiguration, ILauncherService? launcherService = null)
        {
            return loggerConfiguration.Sink(new GrpcSink(packageConfiguration, launcherService));
        }
    }

    public class GrpcSink : ILogEventSink
    {
        private readonly ILauncherService? _launcherService;
        private readonly CompactJsonFormatter _formatter;
        private readonly PackageConfiguration _packageConfiguration;

        public GrpcSink(PackageConfiguration packageConfiguration, ILauncherService? launcherService = null)
        {
            _formatter = new CompactJsonFormatter();
            _launcherService = launcherService;
            _packageConfiguration = packageConfiguration;
        }

        public void Emit(LogEvent logEvent)
        {
            if (_launcherService is not null)
            {
                var writer = new StringWriter();
                _formatter.Format(logEvent, writer);
                writer.Flush();

                try
                {
                    _launcherService!.Log(new LogEventDto
                    {
                        LogEvent = writer.ToString(),
                        Enrichment = new()
                        {
                            { "EnrichedId", _packageConfiguration.Id.ToString() },
                            { "EnrichedPackage", _packageConfiguration.Package }
                        }
                    });
                }
                catch { }
            }
        }
    }
}
