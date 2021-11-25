
using System.ServiceModel;
using System.Text.Json;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.Logging;
using fiskaltrust.Launcher.ProcessHost;
using Serilog.Events;
using Serilog.Formatting.Compact.Reader;

namespace fiskaltrust.Launcher.Services
{
    [ServiceContract]
    public class ProcessHostService : IProcessHostService
    {
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;
        private readonly Serilog.ILogger _logger;

        public ProcessHostService(Dictionary<Guid, ProcessHostMonarch> hosts, Serilog.ILogger logger)
        {
            _hosts = hosts;
            _logger = logger;
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
            var reader = new LogEventReader(new StringReader(payload.LogEvent));

            if (reader.TryRead(out var logEvent))
            {
                _logger.Write(logEvent);
                // _hosts[Guid.Parse(id)].Log(logEvent);
            }
            return Task.CompletedTask;
        }
    }
}
