using FluentAssertions;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Extensions;
using AutoBogus;
using Microsoft.AspNetCore.DataProtection;

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

            var dataProtector = DataProtectionExtensions.Create(launcherConfiguration.AccessToken).CreateProtector("fiskaltrust.Launcher.Configuration");

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