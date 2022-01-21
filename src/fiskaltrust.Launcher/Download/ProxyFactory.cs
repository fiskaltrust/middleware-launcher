using System.Net;

namespace fiskaltrust.Launcher.Download
{
    public static class ProxyFactory
    {
        public static WebProxy? CreateProxy(string? proxyString)
        {
            if (proxyString != null)
            {
                string address = string.Empty;
                bool bypasslocalhost = true;
                List<string> bypass = new();
                string username = string.Empty;
                string password = string.Empty;

                if (proxyString.ToLower() == "off")
                {
                    return new WebProxy();
                }
                else
                {

                    foreach (string keyvalue in proxyString.Split(new char[] { ';' }))
                    {
                        var data = keyvalue.Split(new char[] { '=' });
                        if (data.Length < 2)
                        {
                            continue;
                        }

                        switch (data[0].ToLower().Trim())
                        {
                            case "address": address = data[1]; break;
                            case "bypasslocalhost": if (!bool.TryParse(data[1], out bypasslocalhost)) { bypasslocalhost = false; } break;
                            case "bypass": bypass.Add(data[1]); break;
                            case "username": username = data[1]; break;
                            case "password": password = data[1]; break;
                            default: break;
                        }
                    }

                    WebProxy? proxy;

                    if (!string.IsNullOrWhiteSpace(address))
                    {
                        proxy = new WebProxy(address, bypasslocalhost, bypass.ToArray());
                    }
                    else
                    {
                        return null;
                    }

                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        proxy.UseDefaultCredentials = false;
                        proxy.Credentials = new NetworkCredential(username, password);
                    }

                    return proxy;
                }
            }

            return null;
        }
    }
}
