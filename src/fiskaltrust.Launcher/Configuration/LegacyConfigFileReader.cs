using fiskaltrust.Launcher.Common.Configuration;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Xml.Linq;

namespace fiskaltrust.Launcher.Configuration
{
    public class LegacyConfigFileReader
    {
        public static async Task<LauncherConfiguration> ReadLegacyConfigFile(string path)
        {
            using var fsSource = new FileStream(path, FileMode.Open, FileAccess.Read);
            var launcherConfiguration = new LauncherConfiguration(true);
            try
            {
                XElement purchaseOrder = await XElement.LoadAsync(fsSource, LoadOptions.None, CancellationToken.None);
                var appSettings = from item in purchaseOrder.Descendants("appSettings").DescendantsAndSelf("add")
                                  select item;

                foreach (var item in appSettings)
                {
                    var key = item.Attribute("key")?.Value;
                    var value = item.Attribute("value")?.Value;
                    if (key is not null && value is not null)
                    {
                        SetProperies(launcherConfiguration, key!, value!);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error when reading legacy config file {path}.", path);
            }
            finally
            {
                fsSource.Close();
            }
            Log.Information("Read legacy config file {path}.", path);
            return launcherConfiguration;
        }

        private static void SetProperies(LauncherConfiguration launcherConfiguration, string key, string value)
        {
            if (key == "cashboxid")
            {
                launcherConfiguration.CashboxId = Guid.Parse(value);
            }
            else if (key == "accesstoken")
            {
                launcherConfiguration.AccessToken = value;
            }
            else if (key == "useoffline")
            {
                launcherConfiguration.UseOffline = bool.Parse(value);
            }
            else if (key == "servicefolder")
            {
                launcherConfiguration.ServiceFolder = value;
            }
            else if (key == "sandbox")
            {
                launcherConfiguration.Sandbox = bool.Parse(value);
            }
            else if (key == "sslvalidation")
            {
                launcherConfiguration.SslValidation = bool.Parse(value);
            }
            else if (key == "logfile")
            {
                launcherConfiguration.LogFolder = Path.GetDirectoryName(value);
            }
            else if (key == "loglevel")
            {
                launcherConfiguration.LogLevel = Enum.Parse<LogLevel>(value);
            }
            else if (key == "connectiontimeout")
            {
                launcherConfiguration.DownloadTimeoutSec = int.Parse(value);
            }
            else if (key == "connectionretry")
            {
                launcherConfiguration.DownloadRetry = int.Parse(value);
            }
            else if (key == "proxy")
            {
                launcherConfiguration.Proxy = value;
            }
            else if (key == "enablebus")
            {
                launcherConfiguration.EnableBus = bool.Parse(value);
            }
        }
    }
}
