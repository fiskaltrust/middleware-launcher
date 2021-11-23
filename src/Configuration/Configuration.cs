using System.Text.Json.Serialization;

namespace fiskaltrust.Launcher.Configuration
{
    public record LauncherConfiguration
    {
        [JsonPropertyName("ftCashBoxId")]
        public Guid? CashboxId { get; set; }

        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("launcherPort")]
        public int? LauncherPort { get; set; }

        [JsonPropertyName("serviceFolder")]
        public string? ServiceFolder { get; set; }

        
        [JsonPropertyName("sandbox")]
        public bool? Sandbox { get; set; } = false;
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
