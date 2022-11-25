using fiskaltrust.Launcher.Common.Helpers;
using FluentAssertions;

namespace fiskaltrust.Launcher.UnitTest.Helpers
{
    public class AesHelperTests
    {
        [Fact]
        public void EncryptDecrypt_Should_Return_OriginalValue()
        {
            var inputText = "Hello, World!";
            var key = Convert.FromBase64String("BCYOqMDlzarfanZGTPu0AoIe7sKmCd8xARjYZqx7wf2V42bbul4wCEw51JUAWFQ5l7YLx6kuX7sLPwxaK6cEuq4=");

            var encrypted = AesHelper.Encrypt(inputText, key);
            encrypted.Should().NotBeNullOrEmpty();

            var decrypted = AesHelper.Decrypt(Convert.ToBase64String(encrypted), key);
            decrypted.Should().NotBeNullOrEmpty();
            decrypted.Should().Be(inputText);
        }
    }
}
