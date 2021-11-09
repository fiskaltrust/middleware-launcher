
using System.Runtime.Serialization;
using System.ServiceModel;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.Interfaces
{
    [ServiceContract]
    public interface IProcessHostService {
        [OperationContract]
        Task Started(string id);

        [OperationContract]
        Task Ping();
    }
}
