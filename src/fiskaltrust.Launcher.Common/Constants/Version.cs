using System.Reflection;

namespace fiskaltrust.Launcher.Common.Constants
{
    public static class Version
    {
        public static SemanticVersioning.Version? CurrentVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return version is not null ? new SemanticVersioning.Version(version) : null;
            }
        }
    }
}
