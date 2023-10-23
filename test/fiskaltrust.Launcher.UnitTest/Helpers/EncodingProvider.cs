using System.Text;
using fiskaltrust.Launcher.Common;
using fiskaltrust.Launcher.Services;
using FluentAssertions;
using Xunit;

namespace fiskaltrust.Launcher.UnitTest.Helpers
{
  public class LauncherEncodingProviderTest
  {
    [Fact]
    public void GetEncoding_ReturnsUtf8Encoding()
    {
      var provider = new LauncherEncodingProvider();

      Encoding.RegisterProvider(provider);

      provider.GetEncoding("\"UTF-8\"").Should().Be(Encoding.UTF8);
      provider.GetEncoding("UTF-8").Should().Be(null);

      Encoding.GetEncoding("\"UTF-8\"").Should().Be(Encoding.UTF8);
      Encoding.GetEncoding("UTF-8").Should().Be(Encoding.UTF8);
    }
  }
}