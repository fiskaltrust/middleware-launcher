using System.Reflection;
using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Constants;

namespace fiskaltrust.Launcher.Configuration
{
    public record LauncherConfiguration
    {
        private Guid? _cashboxId;
        [JsonPropertyName("ftCashBoxId")]
        public Guid? CashboxId { get => _cashboxId; set => _cashboxId = value; }

        private string? _accessToken;
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get => _accessToken; set => _accessToken = value; }

        private int? _launcherPort;
        [JsonPropertyName("launcherPort")]
        public int LauncherPort { get => _launcherPort ?? 3000; set => _launcherPort = value; }

        private string? _serviceFolder;
        [JsonPropertyName("serviceFolder")]
        public string ServiceFolder { get => _serviceFolder ?? Paths.ServiceFolder; set => _serviceFolder = value; }

        private bool? _sandbox;
        [JsonPropertyName("sandbox")]
        public bool Sandbox { get => _sandbox ?? false; set => _sandbox = value; }

        private bool? _useOffline;
        [JsonPropertyName("useOffline")]
        public bool UseOffline { get => _useOffline ?? false; set => _useOffline = value; }

        private string? _logFolder;
        [JsonPropertyName("logFolder")]
        public string LogFolder { get => _logFolder ?? Path.Combine(ServiceFolder, "logs"); set => _logFolder = value; }

        private LogLevel? _logLevel;
        [JsonPropertyName("logLevel")]
        public LogLevel LogLevel { get => _logLevel ?? LogLevel.Information; set => _logLevel = value; }

        private Uri? _packagesUrl;
        [JsonPropertyName("packagesUrl")]
        public Uri PackagesUrl { get => _packagesUrl ?? new Uri(Sandbox ? "https://packages-sandbox.fiskaltrust.cloud" : "https://packages.fiskaltrust.cloud"); set => _packagesUrl = value; }

        private Uri? _helipadUrl;
        [JsonPropertyName("helipadUrl")]
        public Uri HelipadUrl { get => _helipadUrl ?? new Uri(Sandbox ? "https://helipad-sandbox.fiskaltrust.cloud" : "https://helipad.fiskaltrust.cloud"); set => _helipadUrl = value; }

        private int? _downloadTimeoutSec;
        [JsonPropertyName("downloadTimeout")]
        public int DownloadTimeoutSec { get => _downloadTimeoutSec ?? 15; set => _downloadTimeoutSec = value; } // TODO implement

        private int? _downloadRetry;
        [JsonPropertyName("downloadRetry")]
        public int DownloadRetry { get => _downloadRetry ?? 1; set => _downloadRetry = value; } // TODO implement

        private bool? _sslValidation;
        [JsonPropertyName("sslValidation")]
        public bool SslValidation { get => _sslValidation ?? true; set => _sslValidation = value; } // TODO implement

        private string? _proxy = null;
        [JsonPropertyName("proxy")]
        public string? Proxy { get => _proxy; set => _proxy = value; }

        private int? _processHostPingPeriodSec;
        [JsonPropertyName("processHostPingPeriodSec")]
        public int ProcessHostPingPeriodSec { get => _processHostPingPeriodSec ?? 10; set => _processHostPingPeriodSec = value; }

        private string? _cashboxConfiguration;
        [JsonPropertyName("cashboxConfigurationFile")]
        public string CashboxConfigurationFile { get => _cashboxConfiguration ?? Path.Join(ServiceFolder, "service", CashboxId!.ToString(), "configuration.json"); set => _cashboxConfiguration = value; }

        public void OverwriteWith(LauncherConfiguration? source)
        {
            if(source == null) { return; }

            foreach (var field in typeof(LauncherConfiguration).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = field.GetValue(source);

                if (value != null)
                {
                    field.SetValue(this, value);
                }
            }
        }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
