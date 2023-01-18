using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.IntegrationTest
{
    public static class TestLauncherConfig
    {
        public static LauncherConfiguration GetTestLauncherConfig(Guid? cashboxId = null, string? accessToken = null)
        {
            var launcherConfiguration = new LauncherConfiguration()
            {
                ServiceFolder = AppDomain.CurrentDomain.BaseDirectory,
                CashboxId = cashboxId ?? Guid.Parse("f3661e3c-5101-4d77-9396-c6cfc5d01a2c"),
                AccessToken = accessToken ?? "BOQoYvuEFULhg/NFfkQ3kzrwOGdZRxgFYhjH59c8Fk93kA8EJeVRef013g3XZUq1cxJx6dDOGyi9QodBTDLHGpo=",
                Sandbox = true
            };
            return launcherConfiguration;
        }
    }
}
