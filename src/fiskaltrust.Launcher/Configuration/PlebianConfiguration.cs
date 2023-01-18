using System.Text.Json;
using fiskaltrust.Launcher.Constants;

namespace fiskaltrust.Launcher.Configuration
{
    public record PlebianConfiguration
    {
        public PackageType PackageType { get; set; }
        public Guid PackageId { get; set; }

        public static PlebianConfiguration Deserialize(string text) => JsonSerializer.Deserialize(text, typeof(PlebianConfiguration), Helpers.Serialization.SerializerContext.Default) as PlebianConfiguration ?? throw new Exception($"Could not deserialize {nameof(PlebianConfiguration)}");

        public string Serialize() => JsonSerializer.Serialize(this, typeof(PlebianConfiguration), Helpers.Serialization.SerializerContext.Default);
    }
}
