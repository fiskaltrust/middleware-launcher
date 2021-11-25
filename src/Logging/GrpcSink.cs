
using System.Runtime.Serialization;
using System.Text.Json;
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
        public static LoggerConfiguration GrpcSink(this LoggerSinkConfiguration loggerConfiguration, IProcessHostService? processHostService, PackageConfiguration packageConfiguration)
        {
            return loggerConfiguration.Sink(new GrpcSink(processHostService, packageConfiguration));
        }
    }

    public class GrpcSink : ILogEventSink
    {
        private readonly IProcessHostService? _processHostService;
        private readonly CompactJsonFormatter _formatter;
        private readonly PackageConfiguration _packageConfiguration;

        public GrpcSink(IProcessHostService? processHostService, PackageConfiguration packageConfiguration)
        {
            _formatter = new CompactJsonFormatter();
            _processHostService = processHostService;
            _packageConfiguration = packageConfiguration;
        }

        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            writer.Flush();
            _processHostService?.Log(new LogEventDto
            {
                Id = _packageConfiguration.Id.ToString(),
                LogEvent = writer.ToString()
            });
        }
    }
}
