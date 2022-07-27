using System.Reflection;
using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Common.Constants;
using Microsoft.Extensions.Logging;
using Semver;

namespace fiskaltrust.Launcher.Common.Configuration
{
    public record LauncherConfiguration
    {
        private bool _useDefaults;

        [JsonConstructor]
        public LauncherConfiguration() { _useDefaults = false; }

        public LauncherConfiguration(bool useDefaults = true)
        {
            _useDefaults = useDefaults;
        }

        public void EnableDefaults()
        {
            _useDefaults = true;
        }

        public void DisableDefaults()
        {
            _useDefaults = false;
        }

        private T WithDefault<T>(T value, T defaultValue)
        {
            if (!_useDefaults)
            {
                return value;
            }
            return value ?? defaultValue;
        }

        private T WithDefault<T>(T value, Func<T> defaultValue)
        {
            if (!_useDefaults)
            {
                return value;
            }
            return value ?? defaultValue();
        }

        private Guid? _cashboxId;
        [JsonPropertyName("ftCashBoxId")]
        public Guid? CashboxId { get => _cashboxId; set => _cashboxId = value; }

        private string? _accessToken;
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get => _accessToken; set => _accessToken = value; }

        private int? _launcherPort;
        [JsonPropertyName("launcherPort")]
        public int? LauncherPort { get => WithDefault(_launcherPort, 5050); set => _launcherPort = value; }

        private string? _serviceFolder;
        [JsonPropertyName("serviceFolder")]
        public string? ServiceFolder { get => WithDefault(_serviceFolder, Paths.ServiceFolder); set => _serviceFolder = value; }

        private bool? _sandbox;
        [JsonPropertyName("sandbox")]
        public bool? Sandbox { get => WithDefault<bool?>(_sandbox.GetValueOrDefault(false) ? true : null, false); set => _sandbox = value; }

        private bool? _useOffline;
        [JsonPropertyName("useOffline")]
        public bool? UseOffline { get => WithDefault<bool?>(_useOffline.GetValueOrDefault(false) ? true : null, false); set => _useOffline = value; }

        private string? _logFolder;
        [JsonPropertyName("logFolder")]
        public string? LogFolder { get => WithDefault(_logFolder, () => Path.Combine(ServiceFolder!, "logs")); set => _logFolder = value; }

        private LogLevel? _logLevel;
        [JsonPropertyName("logLevel")]
        public LogLevel? LogLevel { get => WithDefault(_logLevel, Microsoft.Extensions.Logging.LogLevel.Information); set => _logLevel = value; }

        private Uri? _packagesUrl;
        [JsonPropertyName("packagesUrl")]
        public Uri? PackagesUrl { get => WithDefault(_packagesUrl, () => new Uri(Sandbox!.Value ? "https://packages-2-0-sandbox.fiskaltrust.cloud" : "https://packages-2-0.fiskaltrust.cloud")); set => _packagesUrl = value; }

        private Uri? _helipadUrl;
        [JsonPropertyName("helipadUrl")]
        public Uri? HelipadUrl { get => WithDefault(_helipadUrl, () => new Uri(Sandbox!.Value ? "https://helipad-sandbox.fiskaltrust.cloud" : "https://helipad.fiskaltrust.cloud")); set => _helipadUrl = value; }

        private Uri? _configurationUrl;
        [JsonPropertyName("configurationUrl")]
        public Uri? ConfigurationUrl { get => WithDefault(_configurationUrl, () => new Uri(Sandbox!.Value ? "https://configuration-sandbox.fiskaltrust.cloud" : "https://configuration.fiskaltrust.cloud")); set => _configurationUrl = value; }

        private int? _downloadTimeoutSec;
        [JsonPropertyName("downloadTimeoutSec")]
        public int? DownloadTimeoutSec { get => WithDefault(_downloadTimeoutSec, 15); set => _downloadTimeoutSec = value; } // TODO implement

        private int? _downloadRetry;
        [JsonPropertyName("downloadRetry")]
        public int? DownloadRetry { get => WithDefault(_downloadRetry, 1); set => _downloadRetry = value; } // TODO implement

        private bool? _sslValidation;
        [JsonPropertyName("sslValidation")]
        public bool? SslValidation { get => WithDefault<bool?>(_sslValidation.GetValueOrDefault(false) ? true : null, true); set => _sslValidation = value; } // TODO implement

        private string? _proxy = null;
        [JsonPropertyName("proxy")]
        public string? Proxy { get => _proxy; set => _proxy = value; }

        private int? _processHostPingPeriodSec;
        [JsonPropertyName("processHostPingPeriodSec")]
        public int? ProcessHostPingPeriodSec { get => WithDefault(_processHostPingPeriodSec, 10); set => _processHostPingPeriodSec = value; }

        private string? _cashboxConfiguration;
        [JsonPropertyName("cashboxConfigurationFile")]
        public string? CashboxConfigurationFile { get => WithDefault(_cashboxConfiguration, () => Path.Join(ServiceFolder, "service", $"Configuration-{CashboxId}.json")); set => _cashboxConfiguration = value; }

        private SemVersion? _launcherVersion = null;
        [JsonPropertyName("launcherVersion")]
        [JsonConverter(typeof(SemVersionConverter))]
        public SemVersion? LauncherVersion { get => _launcherVersion; set => _launcherVersion = value?.PrecedenceEquals(new SemVersion(0)) ?? true ? null : value; }

        public void OverwriteWith(LauncherConfiguration? source)
        {
            if (source is null) { return; }

            foreach (var field in typeof(LauncherConfiguration).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = field.GetValue(source);

                if (value is not null)
                {
                    field.SetValue(this, value);
                }
            }
        }

        public LauncherConfiguration Redacted()
        {
            var redacted = (LauncherConfiguration)MemberwiseClone();

            redacted.AccessToken = "<redacted>";

            return redacted;
        }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }
    }
}
