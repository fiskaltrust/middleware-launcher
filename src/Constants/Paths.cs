namespace fiskaltrust.Launcher.Constants {
  public static class Paths {
    public static string ServiceFolder => (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS()) switch {
      (true, false, false) => "C:/ProgramData/fiskaltrust",
      (false, true, false) => "/var/lib/fiskaltrust",
      (false, false, true) => "/Library/Application Support/fiskaltrust",
      _ => "/var/lib/fiskaltrust"
    };
  }
}
