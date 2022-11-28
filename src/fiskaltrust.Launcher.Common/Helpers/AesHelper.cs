using System.Security.Cryptography;

namespace fiskaltrust.Launcher.Common.Helpers
{
    public static class AesHelper
    {
        public static byte[] Encrypt(string plainText, byte[] key)
        {
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException(nameof(plainText));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));

            using var aes = Aes.Create();
            aes.Key = key.Take(16).ToArray();
            aes.IV = Guid.NewGuid().ToByteArray();

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using var ms = new MemoryStream();
            ms.Write(aes.IV);

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            
            return ms.ToArray();
        }

        public static string Decrypt(string secretBase64, byte[] key)
        {
            if (string.IsNullOrEmpty(secretBase64))
                throw new ArgumentNullException(nameof(secretBase64));
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException(nameof(key));

            var secret = Convert.FromBase64String(secretBase64);

            using var aes = Aes.Create();
            aes.Key = key.Take(16).ToArray();
            aes.IV = secret.Take(16).ToArray();

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using var ms = new MemoryStream(secret.Skip(16).ToArray());
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
    }
}
