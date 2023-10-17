using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fiskaltrust.Launcher.Helpers
{
    public class NumberToStringConverter : JsonConverter<string>
    {
        private readonly static JsonConverter<string> DEFAULT_CONVERTER = (JsonConverter<string>)JsonSerializerOptions.Default.GetConverter(typeof(string));

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long number))
                {
                    return number.ToString(CultureInfo.InvariantCulture);
                }

                if (reader.TryGetDecimal(out var doubleNumber))
                {
                    return doubleNumber.ToString(CultureInfo.InvariantCulture);
                }
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => DEFAULT_CONVERTER.Write(writer, value, options);
    }
}