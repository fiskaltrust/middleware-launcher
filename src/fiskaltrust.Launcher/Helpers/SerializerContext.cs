using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Configuration;

namespace fiskaltrust.Launcher.Helpers.Serialization
{
    [JsonSerializable(typeof(PlebianConfiguration))]
    public partial class SerializerContext : JsonSerializerContext { }
}