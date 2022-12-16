using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using fiskaltrust.Launcher.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;

namespace fiskaltrust.Launcher.Extensions
{

    static class KeyUtils
    {
        [DllImport("keyutils", SetLastError = true)]
        public static extern Int32 add_key(IntPtr type, IntPtr description, IntPtr payload, UIntPtr plen, Int32 keyring);

        [DllImport("keyutils", SetLastError = true)]
        public static extern Int64 keyctl_read(Int32 key, IntPtr buffer, UIntPtr buflen);

        [DllImport("keyutils", SetLastError = true)]
        public static extern Int32 keyctl_get_keyring_ID(Int32 id, bool create);
    }

    class KeyringXmlEncryptor : IXmlEncryptor
    {
        public const int KEY_SPEC_USER_KEYRING = -4;
        public const long SYS_keyctl = 250;

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            var keyringId = KeyUtils.keyctl_get_keyring_ID(KEY_SPEC_USER_KEYRING, false);
            if (keyringId < 0)
            {
                throw new Exception($"Could not get keyring: errno {Marshal.GetLastPInvokeError()}");
            }

            Log.Warning("p {l}", plaintextElement.ToString());
            var plaintextElementBytes = Encoding.Unicode.GetBytes(plaintextElement.ToString());
            var plaintextElementPtr = Marshal.AllocHGlobal(plaintextElementBytes.Length);
            Marshal.Copy(plaintextElementBytes, 0, plaintextElementPtr, plaintextElementBytes.Length);

            var type = Encoding.ASCII.GetBytes("user");
            var typePtr = Marshal.AllocHGlobal(type.Length);
            Marshal.Copy(type, 0, typePtr, type.Length);

            var description = Encoding.ASCII.GetBytes("fiskaltrust.Launcher DataProtection Key");
            var descriptionPtr = Marshal.AllocHGlobal(description.Length);
            Marshal.Copy(description, 0, descriptionPtr, description.Length);

            var keySerial = KeyUtils.add_key(typePtr, descriptionPtr, plaintextElementPtr, (UIntPtr)plaintextElementBytes.Length, keyringId);
            if (keySerial < 0)
            {
                throw new Exception($"Could not save key in keyring: errno {Marshal.GetLastPInvokeError()}");
            }
            var encryptedElement = new XElement("key_serial", keySerial.ToString());

            Log.Warning("e {l}", encryptedElement.ToString());
            Marshal.FreeHGlobal(plaintextElementPtr);
            Marshal.FreeHGlobal(typePtr);
            Marshal.FreeHGlobal(descriptionPtr);

            return new EncryptedXmlInfo(encryptedElement, typeof(KeyringXmlDecryptor));
        }
    }

    class KeyringXmlDecryptor : IXmlDecryptor
    {
        public const long SYS_keyctl = 250;
        public const int KEYCTL_READ = 11;
        public const int MAX_CAPACITY = 32767;

        public KeyringXmlDecryptor(IServiceCollection _)
        {
        }

        public KeyringXmlDecryptor()
        {
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            var bufferPtr = Marshal.AllocHGlobal(MAX_CAPACITY);
            var len = KeyUtils.keyctl_read(Int32.Parse(encryptedElement.Value), bufferPtr, MAX_CAPACITY);
            Log.Warning("d {l}", encryptedElement.ToString());

            if (len <= 0)
            {
                throw new Exception($"Could not find key in keyring: errno {Marshal.GetLastPInvokeError()}");
            }
            var buffer = new byte[len];

            Marshal.Copy(bufferPtr, buffer, 0, (int)len);
            Marshal.FreeHGlobal(bufferPtr);

            return XElement.Parse(Encoding.Unicode.GetString(buffer));

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

            var encryptedElement = new XElement("encrypted", Convert.ToBase64String(AesHelper.Encrypt(plaintextElementString, Convert.FromBase64String(accessToken.AccessToken))));

            return new EncryptedXmlInfo(encryptedElement, typeof(LegacyXmlDecryptor));
        }
    }

    class LegacyXmlDecryptor : IXmlDecryptor
    {
        private readonly AccessTokenForEncryption accessToken;

        public LegacyXmlDecryptor(IServiceCollection? services)
        {
            accessToken = services?.BuildServiceProvider()?.GetService<AccessTokenForEncryption>() ?? DataProtectionExtensions.AccessTokenForEncryption!;
        }

        public LegacyXmlDecryptor()
        {
            accessToken = DataProtectionExtensions.AccessTokenForEncryption!; // Because ctor injection is not working with IXmlDecryptor we have to use this workaround (See DataProtectionExtensions.AccessTokenForEncryption declaration below)
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            return XElement.Parse(AesHelper.Decrypt(encryptedElement.Value, Convert.FromBase64String(accessToken.AccessToken)));
        }
    }

    record AccessTokenForEncryption(string AccessToken);

    public static class DataProtectionExtensions
    {
        internal static AccessTokenForEncryption? AccessTokenForEncryption = null; // This godawful workaround exists becaues of this allegedly fixed bug https://github.com/dotnet/aspnetcore/issues/2523

        public static IDataProtectionProvider Create(string? accessToken = null) =>
            DataProtectionProvider
            .Create(
                new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "fiskaltrust.Launcher", "keys")),
                configuration =>
                {
                    configuration.SetApplicationName("fiskaltrust.Launcher");
                    configuration.ProtectKeysCustom(accessToken);
                });

        public static IDataProtectionBuilder ProtectKeysCustom(this IDataProtectionBuilder builder, string? accessToken = null)
        {
            if (accessToken is not null)
            {
                builder.Services.AddSingleton(new AccessTokenForEncryption(accessToken));
                AccessTokenForEncryption = new AccessTokenForEncryption(accessToken);
            }

            builder
                .SetDefaultKeyLifetime(DateTime.MaxValue - DateTime.Now) // Encryption fails if we use TimeStamp.MaxValue because that results in a DateTime exceeding its MaxValue ¯\_(ツ)_/¯
                .SetApplicationName("fiskaltrust.Launcher");

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    builder.ProtectKeysWithDpapi(true);
                    return builder;
                }
                catch { }
            }
            else
            {
                try
                {
                    Marshal.PrelinkAll(typeof(KeyUtils));
                    builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new KeyringXmlEncryptor());
                    return builder;
                }
                catch (Exception e)
                {
                    Log.Warning(e, "Fallback config encryption mechanism used.");
                }
            }

            builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new LegacyXmlEncryptor(builder.Services));

            return builder;
        }
    }
}