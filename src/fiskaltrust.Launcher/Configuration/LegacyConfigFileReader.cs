using fiskaltrust.Launcher.Common.Configuration;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace fiskaltrust.Launcher.Configuration
{
    public class LegacyConfigFileReader
    {
        public static async Task<LauncherConfiguration> ReadLegacyConfigFile(List<(LogLevel logLevel, string message, Exception? e)> errors, string path)
        {
            var launcherConfiguration = new LauncherConfiguration(true);
            if (!File.Exists(path))
            {
                return launcherConfiguration;
            }
            try
            {
                using FileStream fsSource = new FileStream(path, FileMode.Open, FileAccess.Read);
                XElement purchaseOrder = await XElement.LoadAsync(fsSource, LoadOptions.None, CancellationToken.None);
                var appSettings = from item in purchaseOrder.Descendants("appSettings").DescendantsAndSelf("add")
                                  select item;
                
                foreach (var item in appSettings)
                {
                    var key = item.Attribute("key")?.Value;
                    var value = item.Attribute("value")?.Value;
                    SetProperies(launcherConfiguration, key, value);
                }
            }catch(Exception e)
            {
                errors.Add((LogLevel.Error, $"Error when reading legacy config file {path}.", e));
            }
            return launcherConfiguration;
        }

        private static void SetProperies(LauncherConfiguration launcherConfiguration, string? key, string? value)
        {
            if(key == "cashboxid")
            {
                launcherConfiguration.CashboxId = Guid.Parse(value);
            }else if (key == "accesstoken")
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
        }
    }
}
