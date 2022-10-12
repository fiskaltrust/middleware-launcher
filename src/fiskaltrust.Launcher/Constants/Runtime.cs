namespace fiskaltrust.Launcher.Constants
{
    public class Runtime
    {
        public static string Identifier
        {
            get
            {
                string runtimeIdentifier = Environment.Is64BitProcess ? "x64" : "x86";
                if (OperatingSystem.IsWindows())
                {
                    runtimeIdentifier = $"win-{runtimeIdentifier}";
                }
                else if (OperatingSystem.IsLinux())
                {
                    runtimeIdentifier = $"linux-{runtimeIdentifier}";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    runtimeIdentifier = $"osx-{runtimeIdentifier}";
                }
                else
                {
                    runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                }
                return runtimeIdentifier;
            }
        }
    }
}
