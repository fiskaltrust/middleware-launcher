using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Helpers.Serialization
{
    [JsonSerializable(typeof(PlebianConfiguration))]
    public partial class SerializerContext : JsonSerializerContext { }
}