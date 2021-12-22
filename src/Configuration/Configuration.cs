using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Constants;

namespace fiskaltrust.Launcher.Configuration
{
    public record LauncherConfiguration
    {
        [JsonPropertyName("ftCashBoxId")]
        public Guid? CashboxId { get; set; }

        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("launcherPort")]
        public int LauncherPort { get; set; } = 3000;

        [JsonPropertyName("serviceFolder")]
        public string ServiceFolder { get; set; } = Paths.ServiceFolder;

        [JsonPropertyName("sandbox")]
        public bool Sandbox { get; set; } = false;

        [JsonPropertyName("useOffline")]
        public bool UseOffline { get; set; } = false;

        private string? _logFolder;
        [JsonPropertyName("logFolder")]
        public string LogFolder { get => _logFolder ?? Path.Combine(ServiceFolder, "logs"); set => _logFolder = value; }

        [JsonPropertyName("logLevel")]
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        private Uri? _packagesUrl;
        [JsonPropertyName("packagesUrl")]
        public Uri PackagesUrl { get => _packagesUrl ?? new Uri(Sandbox ? "https://packages-sandbox.fiskaltrust.cloud" : "https://packages.fiskaltrust.cloud"); set => _packagesUrl = value; }

        private Uri? _helipadUrl;
        [JsonPropertyName("helipadUrl")]
        public Uri HelipadUrl { get => _helipadUrl ?? new Uri(Sandbox ? "https://helipad-sandbox.fiskaltrust.cloud" : "https://helipad.fiskaltrust.cloud"); set => _helipadUrl = value; }

        [JsonPropertyName("downloadTimeout")]
        public int DownloadTimeoutSec { get; set; } = 15; // TODO implement

        [JsonPropertyName("downloadRetry")]
        public int DownloadRetry { get; set; } = 1; // TODO implement

        [JsonPropertyName("sslValidation")]
        public bool SslValidation { get; set; } = true; // TODO implement

        [JsonPropertyName("proxy")]
        public string? Proxy { get; set; } = null;

        [JsonPropertyName("processHostPingPeriodSec")]
        public int ProcessHostPingPeriodSec { get; set; } = 10;

        private string? _cashboxConfiguration;
        [JsonPropertyName("cashboxConfigurationFile")]
        public string CashboxConfigurationFile { get => _cashboxConfiguration ?? Path.Join(ServiceFolder, "service", CashboxId!.ToString(), "configuration.json"); set => _cashboxConfiguration = value; }

    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
