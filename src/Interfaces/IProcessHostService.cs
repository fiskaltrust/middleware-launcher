
using System.ServiceModel;

namespace fiskaltrust.Launcher.Interfaces
{
    [ServiceContract]
    public interface IProcessHostService
    {
        [OperationContract]
        Task Started(string id);

        [OperationContract]
        Task Ping();
    }
}
