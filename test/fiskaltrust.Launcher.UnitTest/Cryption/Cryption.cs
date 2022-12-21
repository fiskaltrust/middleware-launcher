using FluentAssertions;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Extensions;
using AutoBogus;

namespace fiskaltrust.Launcher.UnitTest.Logging
{
    public class CryptionTests
    {
        [Fact]
        public void ConfigurationEncryptDecrypt_ShouldNotChangeConfig()
        {
            var launcherConfiguration = new AutoFaker<LauncherConfiguration>()
                .Configure(builder =>
                    builder
                    .WithSkip<SemanticVersioning.Range>())
                .RuleFor(c => c.CashboxId, f => Guid.NewGuid())
                .RuleFor(c => c.AccessToken, f => Convert.ToBase64String(f.Random.Bytes(33)))
                .Generate();

            var dataProtector = DataProtectionExtensions.Create(launcherConfiguration.AccessToken, "./keys").CreateProtector(LauncherConfiguration.DATA_PROTECTION_DATA_PURPOSE);

            var o = launcherConfiguration.Serialize();

            launcherConfiguration.Encrypt(dataProtector);

            var encrypted = launcherConfiguration.Serialize();

            launcherConfiguration = LauncherConfiguration.Deserialize(encrypted);

            launcherConfiguration.Decrypt(dataProtector);

            var n = launcherConfiguration.Serialize();

            o.Should().BeEquivalentTo(n);
        }
    }
}