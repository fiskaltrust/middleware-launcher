using System.Text.Json;
using System.Text.Json.Serialization;

namespace fiskaltrust.Launcher.Common.Configuration
{
    public class SemVersionConverter : JsonConverter<SemanticVersioning.Range?>
    {
        public override SemanticVersioning.Range? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            return new(value);
        }
        public override void Write(Utf8JsonWriter writer, SemanticVersioning.Range? semVersionValue, JsonSerializerOptions options)
        {
            var value = semVersionValue?.ToString();
            if (string.IsNullOrEmpty(value))
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteRawValue($"\"{value}\"");
            }
        }
    }
}