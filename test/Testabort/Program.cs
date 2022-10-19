using FluentAssertions;
using System.Diagnostics;

var consolePlaceholder = new Process();
var consolePrj = AppDomain.CurrentDomain.BaseDirectory.Replace("Testabort", "ConsolePlaceholder");
var placeholderExe = Path.Combine(consolePrj, "ConsolePlaceholder.exe");
consolePlaceholder.StartInfo.FileName = placeholderExe;
consolePlaceholder.Start();
consolePlaceholder.WaitForExit();


await Task.Delay(1000);

while (Process.GetProcessesByName("fiskaltrust.Launcher").Length > 0)
{
    await Task.Delay(500);
}

while (Process.GetProcessesByName("fiskaltrust.LauncherUpdater.exe").Length > 0)
{
    await Task.Delay(500);
}

var fvi = FileVersionInfo.GetVersionInfo(Path.Combine("C:\\source\\repos\\middleware-launcher\\test\\Result", "fiskaltrust.Launcher.exe"));

fvi.ProductVersion.Should().Be("2.0.0-preview3");