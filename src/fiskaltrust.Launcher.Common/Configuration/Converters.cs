using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fiskaltrust.Launcher.Common.Configuration
{
    public class SemVersionConverter : JsonConverter<SemanticVersioning.Range>
    {
        public override SemanticVersioning.Range Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetString());

        public override void Write(Utf8JsonWriter writer, SemanticVersioning.Range semVersionValue, JsonSerializerOptions options) => writer.WriteStringValue(semVersionValue.ToString());
    }
}