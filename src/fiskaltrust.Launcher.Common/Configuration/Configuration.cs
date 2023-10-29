using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Common.Constants;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

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
    public class EncryptAttribute : Attribute { }

    public record LauncherConfiguration
    {
        public const string DATA_PROTECTION_DATA_PURPOSE = "fiskaltrust.Launcher.Configuration";

        private bool _rawAccess; // Helper field for the Raw access method. If this is true the properties dont return default values
        private readonly object _rawAccessLock = new();

        [JsonConstructor]
        public LauncherConfiguration() { _rawAccess = false; }

        public T Raw<T>(System.Linq.Expressions.Expression<Func<LauncherConfiguration, T>> accessor)
        {
            lock (_rawAccessLock)
            {
                try
                {
                    _rawAccess = true;
                    return accessor.Compile()(this);
                }
                finally
                {
                    _rawAccess = false;
                }
            }
        }

        private T WithDefault<T>(T value, T defaultValue)
        {
            if (_rawAccess)
            {
                return value;
            }
            return value ?? defaultValue;
        }

        private T WithDefault<T>(T value, Func<T> defaultValue)
        {
            if (_rawAccess)
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
        public string? ServiceFolder { get => MakeAbsolutePath(WithDefault(_serviceFolder, Paths.ServiceFolder)); set => _serviceFolder = value; }

        private bool? _sandbox;
        [JsonPropertyName("sandbox")]
        public bool? Sandbox { get => WithDefault(_sandbox, false); set => _sandbox = value; }

        private bool? _useOffline;
        [JsonPropertyName("useOffline")]
        public bool? UseOffline { get => WithDefault(_useOffline, false); set => _useOffline = value; }

        private string? _logFolder;
        [JsonPropertyName("logFolder")]
        public string? LogFolder { get => MakeAbsolutePath(WithDefault(_logFolder, () => Path.Combine(ServiceFolder!, "logs"))); set => _logFolder = value; }

        private string? _packageCache;
        [JsonPropertyName("packageCache")]
        public string? PackageCache { get => MakeAbsolutePath(WithDefault(_packageCache, () => Path.Combine(ServiceFolder!, "cache"))); set => _packageCache = value; }

        private LogLevel? _logLevel;
        [JsonPropertyName("logLevel")]
        [AlternateName("verbosity")]
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
        public int? DownloadTimeoutSec { get => WithDefault(_downloadTimeoutSec, 15); set => _downloadTimeoutSec = value; }

        private int? _downloadRetry;
        [JsonPropertyName("downloadRetry")]
        public int? DownloadRetry { get => WithDefault(_downloadRetry, 1); set => _downloadRetry = value; }

        private bool? _sslValidation;
        [JsonPropertyName("sslValidation")]
        public bool? SslValidation { get => WithDefault(_sslValidation, true); set => _sslValidation = value; }

        [Encrypt]
        private string? _proxy = null;
        [JsonPropertyName("proxy")]
        public string? Proxy { get => _proxy; set => _proxy = value; }

        private string? _tlsCertificatePath;
        [JsonPropertyName("tlsCertificatePath")]
        public string? TlsCertificatePath { get => MakeAbsolutePath(_tlsCertificatePath); set => _tlsCertificatePath = value; }

        private string? _tlsCertificateBase64;
        [JsonPropertyName("tlsCertificateBase64")]
        public string? TlsCertificateBase64 { get => _tlsCertificateBase64; set => _tlsCertificateBase64 = value; }

        private string? _tlsCertificatePassword;
        [JsonPropertyName("tlsCertificatePassword")]
        public string? TlsCertificatePassword { get => _tlsCertificatePassword; set => _tlsCertificatePassword = value; }

        private int? _processHostPingPeriodSec;
        [JsonPropertyName("processHostPingPeriodSec")]
        public int? ProcessHostPingPeriodSec { get => WithDefault(_processHostPingPeriodSec, 10); set => _processHostPingPeriodSec = value; }

        private string? _cashboxConfiguration;
        [JsonPropertyName("cashboxConfigurationFile")]
        public string? CashboxConfigurationFile { get => MakeAbsolutePath(WithDefault(_cashboxConfiguration, () => Path.Join(ServiceFolder, "service", $"Configuration-{CashboxId}.json"))); set => _cashboxConfiguration = value; }

        private bool? _useHttpSysBinding;
        [JsonPropertyName("useHttpSysBinding")]
        public bool? UseHttpSysBinding { get => WithDefault(_useHttpSysBinding, false); set => _useHttpSysBinding = value; }

        private bool? _useLegacyDataProtection;
        [JsonPropertyName("useLegacyDataProtection")]
        public bool? UseLegacyDataProtection { get => WithDefault(_useLegacyDataProtection, false); set => _useLegacyDataProtection = value; }

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
            var configuration = JsonSerializer.Deserialize(
                text,
                typeof(LauncherConfiguration),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                }
            ) as LauncherConfiguration ?? throw new Exception($"Could not deserialize {nameof(LauncherConfiguration)}");
            configuration.SetAlternateNames(text);
            return configuration;
        }

        public string Serialize(bool writeIndented = false, bool useUnsafeEncoding = false)
            => Raw(raw => JsonSerializer.Serialize(
                raw,
                typeof(LauncherConfiguration),
                new SerializerContext(new JsonSerializerOptions
                {
                    WriteIndented = writeIndented,
                    Encoder = useUnsafeEncoding ? JavaScriptEncoder.UnsafeRelaxedJsonEscaping : JavaScriptEncoder.Default,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Converters = {
                        new JsonStringEnumConverter()
                    }
                })
            ));

        internal void SetAlternateNames(string text)
        {
            lock (_rawAccessLock)
            {
                try
                {
                    _rawAccess = true;

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
                finally
                {
                    _rawAccess = false;
                }
            }
        }

        private void MapFieldsWithAttribute<T>(Func<object?, object?> action)
        {
            var errors = new List<Exception>();

            foreach (var field in GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var value = field.GetValue(this);

                if (field.GetCustomAttributes(typeof(T)).Any())
                {
                    try
                    {
                        field.SetValue(this, action(value));
                    }
                    catch (Exception e)
                    {
                        errors.Add(e);
                    }
                }
            }

            if (errors.Any())
            {
                throw new AggregateException(errors);
            }
        }

        public void Encrypt(IDataProtector dataProtector)
        {
            MapFieldsWithAttribute<EncryptAttribute>(value =>
            {
                if (value is null) { return null; }

                return dataProtector.Protect((string)value);
            });
        }

        public void Decrypt(IDataProtector dataProtector)
        {
            MapFieldsWithAttribute<EncryptAttribute>((value) =>
            {
                if (value is null) { return null; }

                return dataProtector.Unprotect((string)value);
            });
        }

        private static string? MakeAbsolutePath(string? path)
        {
            if (path is not null)
            {
                return Path.GetFullPath(path);
            }

            return null;
        }
        public bool UseDomainSockets { get; init; }
        public string? DomainSocketPath { get; init; }
        public bool UseNamedPipes { get; init; }
        public string? NamedPipeName { get; init; }
    }

    public record LauncherConfigurationInCashBoxConfiguration
    {
        [JsonPropertyName("launcher")]
        public LauncherConfiguration? LauncherConfiguration { get; set; }

        public static LauncherConfiguration? Deserialize(string text)
        {
            var configuration = (JsonSerializer.Deserialize(
                text,
                typeof(LauncherConfigurationInCashBoxConfiguration),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() },
                }
            ) as LauncherConfigurationInCashBoxConfiguration ?? throw new Exception($"Could not deserialize {nameof(LauncherConfigurationInCashBoxConfiguration)}")).LauncherConfiguration;
            configuration?.SetAlternateNames(text);
            return configuration;
        }
    }
}
