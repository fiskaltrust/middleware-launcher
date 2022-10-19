using fiskaltrust.Launcher.Common.Configuration;

namespace fiskaltrust.Launcher.IntegrationTest
{
    public static class TestLauncherConfig
    {
        public static LauncherConfiguration GetTestLauncherConfig()
        {
            var launcherConfiguration = new LauncherConfiguration(true);
            launcherConfiguration.ServiceFolder = AppDomain.CurrentDomain.BaseDirectory;
            launcherConfiguration.CashboxId = Guid.Parse("f3661e3c-5101-4d77-9396-c6cfc5d01a2c");
            launcherConfiguration.AccessToken = "BOQoYvuEFULhg/NFfkQ3kzrwOGdZRxgFYhjH59c8Fk93kA8EJeVRef013g3XZUq1cxJx6dDOGyi9QodBTDLHGpo=";
            launcherConfiguration.Sandbox = true;
            return launcherConfiguration;
        }
    }
}
