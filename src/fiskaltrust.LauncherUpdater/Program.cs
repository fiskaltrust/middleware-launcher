using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using Serilog;

var processIdOption = new Option<int>(name: "--launcher-process-id");
var launcherConfiguration = new Option<string>("--launcher-configuration");
var fromOption = new Option<string>(name: "--from");
var toOption = new Option<string>(name: "--to");

var rootCommand = new RootCommand("Updater for the fiskaltrust.Launcher")
{
  processIdOption,
  fromOption,
  toOption,
  launcherConfiguration
};

rootCommand.SetHandler(RootCommandHandler, processIdOption, fromOption, toOption, launcherConfiguration);

try
{
    await rootCommand.InvokeAsync(args);
    return 0;
}
catch (Exception e)
{
    Log.Error(e, "An unhandled exception occured.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

async static Task RootCommandHandler(int processId, string from, string to, string launcherConfigurationBase64)
{
    var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(launcherConfigurationBase64))) ?? throw new Exception($"Could not deserialize {nameof(LauncherConfiguration)}");
    launcherConfiguration.EnableDefaults();

    Log.Logger = new LoggerConfiguration()
        .AddLoggingConfiguration(launcherConfiguration, new[] { "fiskaltrust.LauncherUpdater", launcherConfiguration.CashboxId!.Value.ToString() })
        .Enrich.FromLogContext()
        .CreateLogger();

    var launcherProcess = Process.GetProcessById(processId);

    if (launcherProcess is not null)
    {
        Log.Information("Waiting for launcher to shut down.");
        await launcherProcess.WaitForExitAsync();
    }

    Log.Information("Copying launcher executable from \"{from}\" to \"{to}\".", from, to);
    File.Copy(from, to);

    Log.Information("Launcher update successful");
}