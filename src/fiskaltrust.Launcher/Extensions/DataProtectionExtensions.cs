using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using fiskaltrust.Launcher.Common.Helpers;
using fiskaltrust.Launcher.Constants;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace fiskaltrust.Launcher.Extensions
{

    static class KeyUtils
    {
        [DllImport("keyutils", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern Int32 add_key(string type, string description, string payload, UIntPtr plen, Int32 keyring);

        [DllImport("keyutils", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern Int32 request_key(string type, string description, string callout_info, Int32 dest_keyring);
    }

    class KeyringXmlEncryptor : IXmlEncryptor
    {
        public const int KEY_SPEC_SESSION_KEYRING = -3;

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            var plaintextElementString = plaintextElement.ToString();

            var keySerial = KeyUtils.add_key("user", "fiskaltrust.Launcher DataProtection Key", plaintextElementString, (UIntPtr)plaintextElementString.Length, KEY_SPEC_SESSION_KEYRING);
            var encryptedElement = new XElement("key_serial", keySerial);

            return new EncryptedXmlInfo(encryptedElement, typeof(KeyringXmlDecryptor));
        }
    }

    class KeyringXmlDecryptor : IXmlDecryptor
    {
        public KeyringXmlDecryptor(IServiceCollection _)
        {
        }

        public KeyringXmlDecryptor()
        {
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            throw new NotImplementedException();
        }
    }

    class LegacyXmlEncryptor : IXmlEncryptor
    {
        private readonly AccessTokenForEncryption accessToken;

        public LegacyXmlEncryptor(IServiceCollection services)
        {
            accessToken = services.BuildServiceProvider().GetRequiredService<AccessTokenForEncryption>();
        }

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            var plaintextElementString = plaintextElement.ToString();

            var encryptedElement = new XElement("encrypted", AesHelper.Encrypt(plaintextElementString, Convert.FromBase64String(accessToken.AccessToken)));

            return new EncryptedXmlInfo(encryptedElement, typeof(LegacyXmlDecryptor));
        }
    }

    class LegacyXmlDecryptor : IXmlDecryptor
    {
        private readonly AccessTokenForEncryption accessToken;

        public LegacyXmlDecryptor(IServiceCollection services)
        {
            accessToken = services.BuildServiceProvider().GetRequiredService<AccessTokenForEncryption>();
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            return XElement.Parse(AesHelper.Decrypt(encryptedElement.Value, Convert.FromBase64String(accessToken.AccessToken)));
        }
    }

    record AccessTokenForEncryption(string AccessToken);

    static class DataProtectionExtensions
    {
        public static IDataProtectionProvider Create(Action<IDataProtectionBuilder> configuration) => DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "fiskaltrust.Launcher", "keys")), configuration);

        public static IDataProtectionBuilder ProtectKeysCustom(this IDataProtectionBuilder builder, string? accessToken = null)
        {
            if (accessToken is not null)
            {
                builder.Services.AddSingleton(new AccessTokenForEncryption(accessToken));
            }

            builder
                .SetDefaultKeyLifetime(TimeSpan.MaxValue - TimeSpan.FromDays(1)) // Encryption fails if we use max value directly ¯\_(ツ)_/¯
                .SetApplicationName("fiskaltrust.Launcher");

            if (OperatingSystem.IsWindows())
            {
                builder.ProtectKeysWithDpapi();
            }
            else
            {
                builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new LegacyXmlEncryptor(builder.Services));
                builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new KeyringXmlEncryptor());
            }

            return builder;
        }
    }
}