namespace fiskaltrust.Launcher.Common.Constants
{
    public static class Version
    {
        public static SemanticVersioning.Version? CurrentVersion
        {
            get
            {
                var version = ThisAssembly.AssemblyInformationalVersion;
                return version is not null ? new SemanticVersioning.Version(version) : null;
            }
        }
    }
}
