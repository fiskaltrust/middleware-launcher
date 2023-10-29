using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Configuration;

namespace fiskaltrust.Launcher.Helpers.Serialization
{
    [JsonSerializable(typeof(PlebeianConfiguration))]
    public partial class SerializerContext : JsonSerializerContext { }
}