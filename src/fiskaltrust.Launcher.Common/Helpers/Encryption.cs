using System.Security.Cryptography;
using System.Text;

namespace fiskaltrust.Launcher.Common.Helpers
{
    public class Encryption
    {
        private readonly byte[] _clientSharedSecret;
        private readonly byte[] _iv;

        public Encryption(Guid cashboxId, string accessToken)
        {
            using var serverPublicKeyDh = ParsePublicKey(Convert.FromBase64String(accessToken));
            _clientSharedSecret = CreateCurve().DeriveKeyMaterial(serverPublicKeyDh);

            _iv = cashboxId.ToByteArray();
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

        public static ECDiffieHellman CreateCurve() => ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        public string Decrypt(string value)
        {
            var encrypted = Convert.FromBase64String(value);
            using var aes = Aes.Create();
            aes.Key = _clientSharedSecret;
            var decrypted = aes.DecryptCbc(encrypted, _iv);
            return Encoding.UTF8.GetString(decrypted);
        }

        private string Encrypt(string value, string clientPublicKeyX509, string serverPrivateKeyD, Guid cashBoxId)
        {
            throw new NotImplementedException();
        }
    }
}
