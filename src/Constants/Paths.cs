namespace fiskaltrust.Launcher.Constants
{
    public static class Paths
    {
        public static string ServiceFolder
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return "C:/ProgramData/fiskaltrust";
                }
                else if (OperatingSystem.IsLinux())
                {
                    return "/var/lib/fiskaltrust";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return "/Library/Application Support/fiskaltrust";
                }
                else
                {
                    return "/var/lib/fiskaltrust";
                };
            }
        }
    }
}
