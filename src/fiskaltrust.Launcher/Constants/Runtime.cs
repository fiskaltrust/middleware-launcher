using System.Runtime.InteropServices;

namespace fiskaltrust.Launcher.Constants
{
    public class Runtime
    {
        public static string Identifier
        {
            get
            {
                var arch = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.X86 => "x86",
                    Architecture.Arm64 => "arm64",
                    Architecture.Arm => "arm",
                    _ => throw new NotImplementedException($"The processor architecture {RuntimeInformation.ProcessArchitecture} is currently not supported.")
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return $"win-{arch}";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return $"linux-{arch}";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return $"osx-{arch}";
                }
                else
                {
                    throw new Exception("The Operating System could not be detected.");
                }
            }
        }

        public static bool? IsAdministrator
        {
            get
            {
                if (OperatingSystem.IsWindows())
                {
                    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
                return null;
            }
        }
    }
}
