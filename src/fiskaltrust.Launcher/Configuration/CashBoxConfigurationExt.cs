using System.Security.Cryptography;
using System.Text;
using fiskaltrust.storage.serialization.V0;
using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.Configuration
{
    public static class CashBoxConfigurationExt
    {
        private const string ENCRYPTION_SUFFIX = "_encrypted";
        private static readonly List<string> _configKeyToEncrypt = new() { "connectionstring" };

        public static void Decrypt(this ftCashBoxConfiguration cashboxConfiguration, ECDiffieHellman clientEcdh, LauncherConfiguration launcherConfiguration)
        {
            using var serverPublicKeyDh = ParsePublicKey(Convert.FromBase64String(launcherConfiguration.AccessToken!));

            var sharedSecret = clientEcdh.DeriveKeyMaterial(serverPublicKeyDh);
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
                    queue.Configuration[configKey] = DecryptValue(configString, sharedSecret, iv);
                }
            }
        }

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
            using var aes = Aes.Create();
            aes.Key = clientSharedSecret;
            var decrypted = aes.DecryptCbc(encrypted, iv);
            return Encoding.UTF8.GetString(decrypted);
        }
    }
}
