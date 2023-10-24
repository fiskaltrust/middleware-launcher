using System.Text.Json;
using fiskaltrust.Launcher.Constants;

namespace fiskaltrust.Launcher.Configuration
{
    public record PlebeianConfiguration
    {
        public PackageType PackageType { get; set; }
        public Guid PackageId { get; set; }

        public static PlebeianConfiguration Deserialize(string text) => JsonSerializer.Deserialize(text, typeof(PlebeianConfiguration), Helpers.Serialization.SerializerContext.Default) as PlebeianConfiguration ?? throw new Exception($"Could not deserialize {nameof(PlebeianConfiguration)}");

        public string Serialize() => JsonSerializer.Serialize(this, typeof(PlebeianConfiguration), Helpers.Serialization.SerializerContext.Default);
    }
}
