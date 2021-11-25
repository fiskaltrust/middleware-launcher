
using System.ServiceModel;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.ProcessHost;
using Serilog.Context;
using Serilog.Formatting.Compact.Reader;

namespace fiskaltrust.Launcher.Services
{
    [ServiceContract]
    public class ProcessHostService : IProcessHostService
    {
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly Serilog.ILogger _logger;
        private readonly Mutex _logMutex;

        public ProcessHostService(Dictionary<Guid, ProcessHostMonarch> hosts, Serilog.ILogger logger)
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

                    _logger.Write(logEvent);

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
