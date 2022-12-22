
using System.Runtime.InteropServices;

namespace fiskaltrust.Launcher.IntegrationTest.Helpers
{
    public sealed class FactSkipIf : FactAttribute
    {
        public FactSkipIf(string[] OsIs)
        {
            foreach (var os in OsIs)
            {
                if (os switch
                {
                    "windows" => OperatingSystem.IsWindows(),
                    "linux" => OperatingSystem.IsLinux(),
                    "macos" => OperatingSystem.IsMacOS(),
                    _ => true
                })
                {
                    Skip = $"Ignore on {OsIs}";
                }
            }
        }
    }
}