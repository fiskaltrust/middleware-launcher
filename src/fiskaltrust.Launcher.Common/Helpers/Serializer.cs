using System.Text.Json;
using System.Text.Json.Serialization;

namespace fiskaltrust.Launcher.Common.Helpers.Serialization
{
    public class Serializer
    {
        public static T Deserialize<T>(string from, JsonSerializerContext context) where T : class
        {
            return JsonSerializer.Deserialize(from, typeof(T), context) as T ?? throw new Exception($"Could not deserialize {nameof(T)}");
        }

        public static string Serialize<T>(T from, JsonSerializerContext context) where T : class
        {
            return JsonSerializer.Serialize(from, typeof(T), context);
        }
    }
}