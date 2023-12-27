using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using fiskaltrust.Launcher.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;

namespace fiskaltrust.Launcher.Extensions
{

    unsafe static partial class KeyUtils
    {
        private const Int64 KEYCTL = 250;

        // https://man7.org/linux/man-pages/man3/keyctl_read.3.html
        [DllImport("libc", SetLastError = true, EntryPoint = "syscall")]
        private static extern Int64 keyctl_read(Int64 syscall, Int32 cmd, Int32 key, byte* buffer, UIntPtr buflen);

        private const Int32 KEYCTL_READ = 11;
        private const UInt32 MAX_CAPACITY = 32767;

        public static IEnumerable<byte> Read(Int32 key)
        {
            var buffer = new byte[MAX_CAPACITY];
            fixed (byte* bufferPtr = buffer)
            {
                var len = KeyUtils.keyctl_read(KEYCTL, KEYCTL_READ, key, bufferPtr, new UIntPtr(MAX_CAPACITY));

                if (len <= 0)
                {
                    throw new Exception($"Could not find key in keyring: errno {Marshal.GetLastPInvokeError()}");
                }

                return buffer.Take((int)len);
            }
        }

        // https://man7.org/linux/man-pages/man3/keyctl_get_keyring_ID.3.html
        [DllImport("libc", SetLastError = true, EntryPoint = "syscall")]
        private static extern Int32 keyctl_get_keyring_ID(Int64 syscall, Int32 cmd, Int32 id, [MarshalAs(UnmanagedType.Bool)] bool create);

        private const Int32 KEYCTL_GET_KEYRING_ID = 0;

        public static Int32 GetKeyringId(Int32 id, bool create)
        {
            var keyringId = keyctl_get_keyring_ID(KEYCTL, KEYCTL_GET_KEYRING_ID, id, create);

            if (keyringId < 0)
            {
                throw new Exception($"Could not get linux keyring: errno {Marshal.GetLastPInvokeError()}");
            }

            return keyringId;
        }

        // https://man7.org/linux/man-pages/man2/add_key.2.html
        [DllImport("libc", SetLastError = true, EntryPoint = "syscall", CharSet = CharSet.Ansi)]
        private static extern Int32 add_key(Int64 syscall, string type, string description, byte* payload, UIntPtr plen, Int32 keyring);

        private const Int64 ADD_KEY = 248;

        public static Int32 AddKey(string type, string description, byte[] payload, Int32 keyring)
        {
            fixed (byte* payloadPtr = payload)
            {
                var keyId = KeyUtils.add_key(ADD_KEY, type, description, payloadPtr, (UIntPtr)payload.Length, keyring);

                if (keyId < 0)
                {
                    throw new Exception($"Could not save key in linux keyring: errno {Marshal.GetLastPInvokeError()}");
                }

                return keyId;
            }
        }
    }

    class KeyringXmlEncryptor : IXmlEncryptor
    {
        public const int KEY_SPEC_USER_KEYRING = -4;

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            var keyringId = KeyUtils.GetKeyringId(KEY_SPEC_USER_KEYRING, false);

            var keySerial = KeyUtils.AddKey("user", "fiskaltrust.Launcher DataProtection Key", Encoding.Unicode.GetBytes(plaintextElement.ToString()), keyringId);

            var encryptedElement = new XElement("key_serial", keySerial.ToString());

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
            var buffer = KeyUtils.Read(Int32.Parse(encryptedElement.Value));

            return XElement.Parse(Encoding.Unicode.GetString(buffer.ToArray()));
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
        private const string DATA_PROTECTION_APPLICATION_NAME = "fiskaltrust.Launcher";
        internal static AccessTokenForEncryption? AccessTokenForEncryption = null; // This godawful workaround exists becaues of this allegedly fixed bug https://github.com/dotnet/aspnetcore/issues/2523

        public static IDataProtectionProvider Create(string? accessToken = null, string? path = null, bool useFallback = false) =>
            DataProtectionProvider
            .Create(
                new DirectoryInfo(path ?? Path.Combine(Common.Constants.Paths.CommonFolder, DATA_PROTECTION_APPLICATION_NAME, "keys")),
                configuration =>
                {
                    configuration.SetApplicationName(DATA_PROTECTION_APPLICATION_NAME);
                    configuration.ProtectKeysCustom(accessToken, useFallback);
                });

        public static IDataProtectionBuilder ProtectKeysCustom(this IDataProtectionBuilder builder, string? accessToken = null, bool useFallback = false)
        {
            if (accessToken is not null)
            {
                builder.Services.AddSingleton(new AccessTokenForEncryption(accessToken));
                AccessTokenForEncryption = new AccessTokenForEncryption(accessToken);
            }

            builder
                .SetDefaultKeyLifetime(DateTime.MaxValue - DateTime.Now - TimeSpan.FromDays(1)) // Encryption fails if we use TimeStamp.MaxValue because that results in a DateTime exceeding its MaxValue ¯\_(ツ)_/¯
                .SetApplicationName(DATA_PROTECTION_APPLICATION_NAME);

            if (!useFallback)
            {
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        builder.ProtectKeysWithDpapi(true);
                        return builder;
                    }
                    catch { }
                }
                //else if (OperatingSystem.IsLinux())
                //{
                //    try
                //    {
                //        Marshal.PrelinkAll(typeof(KeyUtils));
                //        builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new KeyringXmlEncryptor());
                //        return builder;
                //    }
                //    catch (Exception e)
                //    {
                //        Log.Warning(e, "Fallback config encryption mechanism used.");
                //    }
                //}
                else if (OperatingSystem.IsMacOS())
                {
                    Log.Warning("Fallback config encryption mechanism is used on macos.");
                }
            }
            builder.Services.Configure<KeyManagementOptions>(options => options.XmlEncryptor = new LegacyXmlEncryptor(builder.Services));

            return builder;
        }
    }
}