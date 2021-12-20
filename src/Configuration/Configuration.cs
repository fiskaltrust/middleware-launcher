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

        private string? _logFolder;
        [JsonPropertyName("logFolder")]
        public string? LogFolder { get => _logFolder ?? (ServiceFolder != null ? Path.Combine(ServiceFolder!, "logs") : null); set => _logFolder = value; }

        [JsonPropertyName("logLevel")]
        public LogLevel? LogLevel { get; set; }

        private Uri? _packagesUrl;
        [JsonPropertyName("packagesUrl")]
        public Uri? PackagesUrl { get => _packagesUrl ?? new Uri((Sandbox ?? false) ? "https://packages-sandbox.fiskaltrust.cloud" : "https://packages.fiskaltrust.cloud"); set => _packagesUrl = value; }

        [JsonPropertyName("downloadTimeout")]
        public int? DownloadTimeoutSec { get; set; }

        [JsonPropertyName("downloadRetry")]
        public int? DownloadRetry { get; set; }

        [JsonPropertyName("sslValidation")]
        public bool? SslValidation { get; set; }

        [JsonPropertyName("proxy")]
        public string? Proxy { get; set; }

        [JsonPropertyName("processHostPingPeriodSec")]
        public int? ProcessHostPingPeriodSec { get; set; }

    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
