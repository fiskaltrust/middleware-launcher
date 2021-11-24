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
        public bool? Sandbox { get; set; }

        [JsonPropertyName("useOffline")]
        public bool? UseOffline { get; set; }

        [JsonPropertyName("logFolder")]
        public string? LogFolder { get; set; }

        [JsonPropertyName("logLevel")]
        public LogLevel? LogLevel { get; set; }

        [JsonPropertyName("packagesUrl")]
        public Uri? PackagesUrl { get; set; }

        [JsonPropertyName("downloadTimeout")]
        public int? DownloadTimeoutSec { get; set; }

        [JsonPropertyName("downloadRetry")]
        public int? DownloadRetry { get; set; }

        [JsonPropertyName("sslValidation")]
        public bool? SslValidation { get; set; }

        [JsonPropertyName("Proxy")]
        public string? Proxy { get; set; }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
