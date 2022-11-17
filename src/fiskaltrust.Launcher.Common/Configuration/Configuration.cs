using System.Dynamic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Common.Constants;
using fiskaltrust.Launcher.Common.Helpers;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using Microsoft.Extensions.Logging;

namespace fiskaltrust.Launcher.Common.Configuration
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class AlternateNameAttribute : Attribute
    {
        public string Name { get; init; }

        public AlternateNameAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class EnctyptAttribute : Attribute { }

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
        [JsonPropertyName("cashboxId")]
        [AlternateName("ftCashBoxId")]
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

        [Enctypt]
        private string? _proxy = null;
        [JsonPropertyName("proxy")]
        public string? Proxy { get => _proxy; set => _proxy = value; }

        private int? _processHostPingPeriodSec;
        [JsonPropertyName("processHostPingPeriodSec")]
        public int? ProcessHostPingPeriodSec { get => WithDefault(_processHostPingPeriodSec, 10); set => _processHostPingPeriodSec = value; }

        private string? _cashboxConfiguration;
        [JsonPropertyName("cashboxConfigurationFile")]
        public string? CashboxConfigurationFile { get => WithDefault(_cashboxConfiguration, () => Path.Join(ServiceFolder, "service", $"Configuration-{CashboxId}.json")); set => _cashboxConfiguration = value; }

        private SemanticVersioning.Range? _launcherVersion = null;
        [JsonPropertyName("launcherVersion")]
        [JsonConverter(typeof(SemVersionConverter))]
        public SemanticVersioning.Range? LauncherVersion { get => _launcherVersion; set => _launcherVersion = (value is null || value == new SemanticVersioning.Range("")) ? null : value; }

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
            redacted.Proxy = redacted.Proxy is null ? null : "<redacted>";

            return redacted;
        }

        public static LauncherConfiguration Deserialize(string text)
        {
            var configuration = JsonSerializer.Deserialize(text, typeof(LauncherConfiguration), SerializerContext.Default) as LauncherConfiguration ?? throw new Exception($"Could not deserialize {nameof(LauncherConfiguration)}");
            configuration.SetAlternateNames(text);
            return configuration;
        }

        public string Serialize(bool writeIndented = false) => JsonSerializer.Serialize(this, typeof(LauncherConfiguration), new SerializerContext(new JsonSerializerOptions { WriteIndented = writeIndented }));

        internal void SetAlternateNames(string text)
        {
            using var configuration = JsonDocument.Parse(text);
            if (configuration is null) { return; }

            foreach (var property in GetType().GetProperties())
            {
                if (property.GetValue(this) is not null)
                {
                    continue;
                }

                var alternateNames = property.GetCustomAttributes<AlternateNameAttribute>().Select(a => a.Name);

                var values = configuration.RootElement.EnumerateObject().Where(property => alternateNames.Contains(property.Name)).Select(property => property.Value).ToList();

                if (values.Count == 0)
                {
                    continue;
                }

                if (values.Count > 1)
                {
                    throw new Exception($"{nameof(LauncherConfiguration)} contained multiple keys which could be use for {property.Name}");
                }

                var type = property.PropertyType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = Nullable.GetUnderlyingType(type) ?? type;
                }

                property.SetValue(this, values[0].Deserialize(type, SerializerContext.Default));
            }
        }

        public void Encrypt(Guid? cashboxId = null, string? accessToken = null)
        {
            var cashboxIdInner = cashboxId.HasValue ? cashboxId.Value : CashboxId;
            if (cashboxIdInner is null)
            {
                throw new Exception("No CashboxId provided.");
            }

            var accessTokenInner = accessToken ?? AccessToken;
            if (accessTokenInner is null)
            {
                throw new Exception("No AccessToken provided.");
            }

            var encryptionHelper = new Encryption(cashboxIdInner.Value, accessTokenInner);

            foreach (var field in GetType().GetFields())
            {
                var value = field.GetValue(this);

                if (value is null)
                {
                    continue;
                }

                if (field.GetCustomAttributes<EnctyptAttribute>().Any())
                {
                    field.SetValue(this, encryptionHelper.Encrypt((string)value));
                }
            }
        }

        public void Decrypt(Guid? cashboxId = null, string? accessToken = null)
        {
            var cashboxIdInner = cashboxId.HasValue ? cashboxId.Value : CashboxId;
            if (cashboxIdInner is null)
            {
                throw new Exception("No CashboxId provided.");
            }

            var accessTokenInner = accessToken ?? AccessToken;
            if (accessTokenInner is null)
            {
                throw new Exception("No AccessToken provided.");
            }

            var encryptionHelper = new Encryption(cashboxIdInner.Value, accessTokenInner);

            foreach (var field in GetType().GetFields())
            {
                var value = field.GetValue(this);

                if (value is null)
                {
                    continue;
                }

                if (field.GetCustomAttributes<EnctyptAttribute>().Any())
                {
                    field.SetValue(this, encryptionHelper.Decrypt((string)value));
                }
            }
        }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }

        public static LauncherConfiguration? Deserialize(string text)
        {
            var configuration = (JsonSerializer.Deserialize(text, typeof(LauncherConfigurationInCashBoxConfiguration), SerializerContext.Default) as LauncherConfigurationInCashBoxConfiguration ?? throw new Exception($"Could not deserialize {nameof(LauncherConfigurationInCashBoxConfiguration)}")).LauncherConfiguration;
            configuration?.SetAlternateNames(text);
            return configuration;
        }
    }
}
