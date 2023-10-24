using System.Text;

namespace fiskaltrust.Launcher.Services
{
  public class LauncherEncodingProvider : EncodingProvider
  {
    public override Encoding? GetEncoding(int codepage) => null;

    // This EncodingProvider needs to be registered in the plebeian processes
    // because ASP.NET Core uses the Encoding.GetEncoding(string) method to parse the charset of the Content-Type header.
    // According to the http standard (https://datatracker.ietf.org/doc/html/rfc7231#section-3.1.1.1) the charset may be wrapped in quotes.
    // Until this is fixed in ASP.NET we'll need the workaround below.
    public override Encoding? GetEncoding(string name)
    {
      try
      {
        if ((name.StartsWith('"') && name.EndsWith('"')) || (name.StartsWith('\'') && name.EndsWith('\'')))
        {
          // This does not lead to an endless recursion, because every time the Encoding.GetEncoding(string) method calls this method either more quotes are trimmed and its recursed or null is returned.
          return Encoding.GetEncoding(name.Substring(1, name.Length - 2));
        }
      }
      catch { }

      return null;
    }
  }
}