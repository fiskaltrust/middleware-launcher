using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Configuration
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
        public int? LauncherPort { get => WithDefault(_launcherPort, 3000); set => _launcherPort = value; }

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
        [JsonPropertyName("downloadTimeout")]
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
        public string? CashboxConfigurationFile { get => WithDefault(_cashboxConfiguration, () => Path.Join(ServiceFolder, "service", CashboxId!.ToString(), "configuration.json")); set => _cashboxConfiguration = value; }

        public void OverwriteWith(LauncherConfiguration? source)
        {
            if (source == null) { return; }

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

    public static class CashBoxConfigurationExt
    {
        private const string ENCRYPTION_SUFFIX = "_encrypted";
        private static readonly List<string> _configKeyToEncrypt = new() { "connectionstring" };

        private static ECDiffieHellmanPublicKey ParsePublicKey(byte[] publicKey)
        {
            byte[] keyX = new byte[publicKey.Length / 2];
            byte[] keyY = new byte[keyX.Length];
            Buffer.BlockCopy(publicKey, 1, keyX, 0, keyX.Length);
            Buffer.BlockCopy(publicKey, 1 + keyX.Length, keyY, 0, keyY.Length);
            ECParameters parameters = new()
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q =
                {
                    X = keyX,
                    Y = keyY,
                },
            };
            using ECDiffieHellman dh = ECDiffieHellman.Create(parameters);
            return dh.PublicKey;
        }

        private static string DecryptValue(string value, byte[] clientSharedSecret, byte[] iv)
        {
            var encrypted = Convert.FromBase64String(value);
            var clientAes = Aes.Create();
            clientAes.Key = clientSharedSecret;
            var decrypted = clientAes.DecryptCbc(encrypted, iv);
            return Encoding.UTF8.GetString(decrypted);
        }

        public static void Decrypt(this ftCashBoxConfiguration cashboxConfiguration, ECDiffieHellman clientEcdh, LauncherConfiguration launcherConfiguration)
        {
            var serverPublicKeyDh = ParsePublicKey(Convert.FromBase64String(launcherConfiguration.AccessToken!));

            var clientSharedSecret = clientEcdh.DeriveKeyMaterial(serverPublicKeyDh);
            var iv = launcherConfiguration.CashboxId!.Value.ToByteArray();

            foreach (var queue in cashboxConfiguration.ftQueues)
            {
                foreach (var configKey in queue.Configuration.Keys.Where(x => _configKeyToEncrypt.Contains(x.ToLower()) || x.ToLower().EndsWith(ENCRYPTION_SUFFIX)))
                {
                    var configString = queue.Configuration[configKey]?.ToString();
                    if (string.IsNullOrEmpty(configString))
                    {
                        continue;
                    }
                    queue.Configuration[configKey] = DecryptValue(configString, clientSharedSecret, iv);
                }
            }

        }
    }
}
