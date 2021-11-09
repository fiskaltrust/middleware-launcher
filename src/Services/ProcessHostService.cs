
using System.ServiceModel;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.ProcessHost;

namespace fiskaltrust.Launcher.Services
{
    [ServiceContract]
    public class ProcessHostService : IProcessHostService {
        private readonly Dictionary<Guid, ProcessHostMonarch> _hosts;

        public ProcessHostService(Dictionary<Guid, ProcessHostMonarch> hosts) {
            _hosts = hosts;
        }

        [OperationContract]
        public Task Started(string id) {
            _hosts[Guid.Parse(id)].Started();
            return Task.CompletedTask;
        }

        [OperationContract]
        public Task Ping() {
            return Task.CompletedTask;
        }
    }
}
