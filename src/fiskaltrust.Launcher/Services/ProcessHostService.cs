
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
        private readonly Mutex _logMutex;

        public ProcessHostService(Dictionary<Guid, IProcessHostMonarch> hosts, Serilog.ILogger logger)
        {
            _hosts = hosts;
            _logger = logger;

            _logMutex = new Mutex();
        }

        [OperationContract]
        public Task Started(string id)
        {
            _hosts[Guid.Parse(id)].Started();
            return Task.CompletedTask;
        }

        [OperationContract]
        public Task Ping()
        {
            return Task.CompletedTask;
        }

        [OperationContract]
        public Task Log(LogEventDto payload)
        {
            _logMutex.WaitOne();
            try
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
                return Task.CompletedTask;
            }
            finally
            {
                _logMutex.ReleaseMutex();
            }
        }
    }
}
