using System.Reflection;
using Semver;

namespace fiskaltrust.Launcher.Constants
{
    public static class Version
    {
        public static SemVersion? CurrentVersion
        {
            get
            {
                var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return version is not null ? SemVersion.Parse(version, SemVersionStyles.Any) : null;
            }
        }
    }
}
