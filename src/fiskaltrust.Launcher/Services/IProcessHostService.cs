
using System.Runtime.Serialization;
using System.ServiceModel;

namespace fiskaltrust.Launcher.Services.Interfaces
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
        void Started(string id);

        [OperationContract]
        void Ping();

        [OperationContract]
        void Log(LogEventDto payload);
    }
}
