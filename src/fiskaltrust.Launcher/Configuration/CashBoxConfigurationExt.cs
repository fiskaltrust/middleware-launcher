using System.Security.Cryptography;
using System.Text;
using fiskaltrust.storage.serialization.V0;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Helpers;
using System.Text.Json;

namespace fiskaltrust.Launcher.Configuration
{
    public static class CashBoxConfigurationExt
    {
        private const string ENCRYPTION_SUFFIX = "_encrypted";
        private static readonly List<string> _configKeyToEncrypt = new() { "connectionstring" };

        public static void Decrypt(this ftCashBoxConfiguration cashboxConfiguration, LauncherConfiguration launcherConfiguration)
        {
            var encryptionHelper = new Encryption(launcherConfiguration.CashboxId!.Value, launcherConfiguration.AccessToken!);

            foreach (var queue in cashboxConfiguration.ftQueues)
            {
                foreach (var configKey in queue.Configuration.Keys.Where(x => _configKeyToEncrypt.Contains(x.ToLower()) || x.ToLower().EndsWith(ENCRYPTION_SUFFIX)))
                {
                    var configString = queue.Configuration[configKey]?.ToString();
                    if (string.IsNullOrEmpty(configString))
                    {
                        continue;
                    }
                    queue.Configuration[configKey] = encryptionHelper.Decrypt(configString);
                }
            }
        }

        public static ftCashBoxConfiguration Deserialize(string text) => JsonSerializer.Deserialize<ftCashBoxConfiguration>(text) ?? throw new Exception($"Could not deserialize {nameof(ftCashBoxConfiguration)}");

        public static string Serialize(this ftCashBoxConfiguration cashboxConfiguration) => JsonSerializer.Serialize(cashboxConfiguration);
    }
}
