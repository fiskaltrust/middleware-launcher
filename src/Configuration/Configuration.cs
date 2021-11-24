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

        [JsonPropertyName("connectionTimeout")]
        public int? ConnectionTimeout { get; set; }

        [JsonPropertyName("connectionRetry")]
        public int? ConnectionRetry { get; set; }

        [JsonPropertyName("sslValidation")]
        public bool? SslValidation { get; set; }

        [JsonPropertyName("proxy")]
        public string? Proxy { get; set; }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
