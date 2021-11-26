
using fiskaltrust.Launcher.Interfaces;
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
        public static LoggerConfiguration GrpcSink(this LoggerSinkConfiguration loggerConfiguration, PackageConfiguration packageConfiguration, IProcessHostService? processHostService = null)
        {
            return loggerConfiguration.Sink(new GrpcSink(packageConfiguration, processHostService));
        }
    }

    public class GrpcSink : ILogEventSink
    {
        private readonly IProcessHostService? _processHostService;
        private readonly CompactJsonFormatter _formatter;
        private readonly PackageConfiguration _packageConfiguration;

        public GrpcSink(PackageConfiguration packageConfiguration, IProcessHostService? processHostService = null)
        {
            _formatter = new CompactJsonFormatter();
            _processHostService = processHostService;
            _packageConfiguration = packageConfiguration;
        }

        public void Emit(LogEvent logEvent)
        {
            if (_processHostService != null)
            {
                var writer = new StringWriter();
                _formatter.Format(logEvent, writer);
                writer.Flush();

                Task.Run(async () => await _processHostService!.Log(new LogEventDto
                {
                    LogEvent = writer.ToString(),
                    Enrichment = new()
                    {
                        { "EnrichedId", _packageConfiguration.Id.ToString() },
                        { "EnrichedPackage", _packageConfiguration.Package }
                    }
                })).GetAwaiter().GetResult();
            }
        }
    }
}
