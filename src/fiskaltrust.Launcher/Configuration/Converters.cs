using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Semver;

namespace fiskaltrust.Launcher.Configuration
{
    public class SemVersionConverter : JsonConverter<SemVersion>
    {
        public override SemVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => SemVersion.Parse(reader.GetString()!, SemVersionStyles.Strict);

        public override void Write(Utf8JsonWriter writer, SemVersion semVersionValue, JsonSerializerOptions options) => writer.WriteStringValue(semVersionValue.ToString());
    }
}