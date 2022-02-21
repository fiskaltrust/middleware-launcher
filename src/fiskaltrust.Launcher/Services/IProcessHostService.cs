
using System.Runtime.Serialization;
using System.ServiceModel;

namespace fiskaltrust.Launcher.Interfaces
{
    [DataContract]
    public class LogEventDto
    {
        [DataMember(Order = 1)]
        public string LogEvent { get; set; } = null!;

        [DataMember(Order = 2)]
        public Dictionary<string, string> Enrichment = new();
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
