using System.Reflection;

namespace fiskaltrust.Launcher.Constants
{
    public static class Version
    {
        public static System.Version? CurrentVersion
        {
            get
            {
                var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return version is not null ? new System.Version(version) : null;
            }
        }
    }
}
