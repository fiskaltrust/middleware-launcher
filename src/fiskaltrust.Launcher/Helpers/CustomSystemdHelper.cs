
namespace fiskaltrust.Launcher.Helpers
{
    public class CustomSystemdHelper
    {
        private static bool? _isSystemdService;

        public static bool IsSystemdService()
            => _isSystemdService ??= GetIsSystemdService();

        private static bool GetIsSystemdService()
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return false;
            }

            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOTIFY_SOCKET")) ||
                   !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LISTEN_PID"));
        }
    }
}
