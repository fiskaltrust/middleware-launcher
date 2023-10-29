
using System.ServiceModel;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services.Interfaces;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;

namespace fiskaltrust.Launcher.Services
{
    [ServiceContract]
    public class ProcessHostService : IProcessHostService
    {
        private readonly Dictionary<Guid, IProcessHostMonarch> _hosts;
        private readonly Serilog.ILogger _logger;
        private readonly object _logLock = new();

        public ProcessHostService(Dictionary<Guid, IProcessHostMonarch> hosts, Serilog.ILogger logger)
        {
            _hosts = hosts;
            _logger = logger;
        }

        [OperationContract]
        public void Started(string id)
        {
            _hosts[Guid.Parse(id)].SetPlebeanStarted();
        }

        [OperationContract]
        public void Ping() { }

        [OperationContract]
        public void Log(LogEventDto payload)
        {
            lock (_logLock)
            {
                var reader = new LogEventReader(new StringReader(payload.LogEvent));

                if (reader.TryRead(out var logEvent))
                {
                    var properties = payload.Enrichment.Select(e => LogContext.PushProperty(e.Key, " " + e.Value)).ToArray();

                    _logger.Write(new LogEvent(
                        logEvent.Timestamp.ToLocalTime(),
                        logEvent.Level,
                        logEvent.Exception,
                        logEvent.MessageTemplate,
                        logEvent.Properties.Select(kv => new LogEventProperty(kv.Key, kv.Value))
                    ));

                    foreach (var p in properties) { p.Dispose(); }
                }
            }
        }
    }
}
