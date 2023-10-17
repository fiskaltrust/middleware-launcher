using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace fiskaltrust.Launcher.Helpers
{
    sealed class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly static JsonConverter<DateTime> DEFAULT_CONVERTER = (JsonConverter<DateTime>)JsonSerializerOptions.Default.GetConverter(typeof(DateTime));
        static readonly DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0);
        static readonly DateTimeOffset EPOCH_OFFSET = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        static readonly Regex REGEX = new Regex("^\\\\?/Date\\(([+-]*\\d+)\\)\\\\?/$", RegexOptions.CultureInvariant);

        static readonly Regex REGEX_OFFSET = new Regex("^\\\\?/Date\\(([+-]*\\d+)([+-])(\\d{2})(\\d{2})\\)\\\\?/$", RegexOptions.CultureInvariant);

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string formatted = reader.GetString()!;

            {
                Match match = REGEX_OFFSET.Match(formatted);

                if (match.Success)
                {
                    return ReadDateTimeOffset(match).DateTime;
                }
            }

            {
                Match match = REGEX.Match(formatted);

                if (match.Success)
                {
                    return ReadDateTime(match);
                }
            }

            return DEFAULT_CONVERTER.Read(ref reader, typeToConvert, options);
        }

        private DateTime ReadDateTime(Match date)
        {
            if (!long.TryParse(date.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixTime))
            {
                throw new JsonException();
            }

            return EPOCH.AddMilliseconds(unixTime);
        }

        private DateTimeOffset ReadDateTimeOffset(Match date)
        {
            if (!long.TryParse(date.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixTime)
                || !int.TryParse(date.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours)
                || !int.TryParse(date.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
            {
                throw new JsonException();
            }

            int sign = date.Groups[2].Value[0] == '+' ? 1 : -1;
            TimeSpan utcOffset = new TimeSpan(hours * sign, minutes * sign, 0);
            return EPOCH_OFFSET.AddMilliseconds(unixTime).ToOffset(utcOffset);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => DEFAULT_CONVERTER.Write(writer, value, options);
    }
}