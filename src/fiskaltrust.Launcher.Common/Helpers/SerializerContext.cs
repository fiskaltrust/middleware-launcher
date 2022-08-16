using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.Common.Helpers.Serialization
{

    [JsonSerializable(typeof(LauncherConfiguration))]
    [JsonSerializable(typeof(LauncherConfigurationInCashBoxConfiguration))]
    public partial class SerializerContext : JsonSerializerContext { }
}