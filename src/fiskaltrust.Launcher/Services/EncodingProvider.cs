using System.Text;

namespace fiskaltrust.Launcher.Services
{
  public class LauncherEncodingProvider : EncodingProvider
  {
    public override Encoding? GetEncoding(int codepage) => CodePagesEncodingProvider.Instance.GetEncoding(codepage);

    public override Encoding? GetEncoding(string name)
    {
      if ((name.StartsWith('"') && name.EndsWith('"')) || (name.StartsWith('\'') && name.EndsWith('\'')))
      {
        // This does not lead to an endless recursion, because every time the Encoding.GetEncoding(string) method calls this method either more quotes are trimmed and its recursed or null is returned.
        return Encoding.GetEncoding(name.Substring(1, name.Length - 2));
      }
      else
      {
        return null;
      }
    }
  }
}