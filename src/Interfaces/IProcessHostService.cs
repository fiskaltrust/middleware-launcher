
using System.Runtime.Serialization;
using System.ServiceModel;

namespace fiskaltrust.Launcher.Interfaces
{
    [DataContract]
    public class LogEventDto
    {
        [DataMember(Order = 1)]
        public string Id { get; set; } = null!;

        [DataMember(Order = 2)]
        public string LogEvent{ get; set; } = null!;
    }


    [ServiceContract]
    public interface IProcessHostService
    {
        [OperationContract]
        Task Started(string id);

        [OperationContract]
        Task Ping();

        [OperationContract]
        Task Log(LogEventDto payload);

    }
}
