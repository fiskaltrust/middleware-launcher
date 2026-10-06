using System.Text.RegularExpressions;

namespace fiskaltrust.Launcher.Common.Constants
{
    public static class Version
    {
        public static SemanticVersioning.Version? CurrentVersion
        {
            get
            {
                var version = ThisAssembly.AssemblyInformationalVersion;

                if (version is null)
                {
                    return null;
                }

                var semVerCompatible = Regex.Replace(version, @"^(\d+\.\d+\.\d+)\.\d+", "$1");

                return new SemanticVersioning.Version(semVerCompatible);
            }
        }
    }
}
