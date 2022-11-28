using System.Security.Cryptography;
using System.Text;

namespace fiskaltrust.Launcher.Helpers
{
    public class CashboxConfigEncryption
    {
        private readonly byte[] _clientSharedSecret;
        private readonly byte[] _iv;
        private readonly ECDiffieHellman _curve;

        public CashboxConfigEncryption(Guid cashboxId, string accessToken, ECDiffieHellman curve)
        {
            _curve = curve;
            using var serverPublicKeyDh = ParsePublicKey(Convert.FromBase64String(accessToken));
            _clientSharedSecret = _curve.DeriveKeyMaterial(serverPublicKeyDh);
            _iv = cashboxId.ToByteArray();
        }

        public static ECDiffieHellman CreateCurve() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        public string Decrypt(string valueBase64)
        {
            var encrypted = Convert.FromBase64String(valueBase64);
            using var aes = Aes.Create();
            aes.Key = _clientSharedSecret;
            var decrypted = aes.DecryptCbc(encrypted, _iv);
            return Encoding.UTF8.GetString(decrypted);
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
    }
}
