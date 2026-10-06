using FluentAssertions;
using Xunit;
using LauncherVersion = fiskaltrust.Launcher.Common.Constants.Version;

namespace fiskaltrust.Launcher.UnitTest.Constants
{
    public class VersionTests
    {
        [Fact]
        public void CurrentVersion_DoesNotThrow()
        {
            var act = () => LauncherVersion.CurrentVersion;

            act.Should().NotThrow();
            LauncherVersion.CurrentVersion.Should().NotBeNull();
        }
    }
}
