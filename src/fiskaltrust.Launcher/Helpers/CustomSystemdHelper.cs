
using fiskaltrust.Launcher.ServiceInstallation;

namespace fiskaltrust.Launcher.Helpers
{
    public class CustomSystemdHelper
    {
        private static bool? _isSystemdService;

        public static bool IsSystemdService(string[] args)
            => _isSystemdService ??= GetIsSystemdServiceAsync(args);

        private static bool GetIsSystemdServiceAsync(string[] args)
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return false;
            }
            return args.Contains("isSystemd");
        }
    }
}
