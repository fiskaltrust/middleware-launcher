namespace fiskaltrust.Launcher.Common.Constants
{
    public static class Paths
    {
        public static string ServiceFolder
        {
            get => Path.Combine(CommonFolder, "fiskaltrust");
        }

        public static string CommonFolder
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    return "C:/ProgramData";
                }
                else if (OperatingSystem.IsLinux())
                {
                    return "/var/lib";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    return "/Library/Application Support";
                }
                else
                {
                    return "/var/lib";
                };
            }
        }
    }
}
