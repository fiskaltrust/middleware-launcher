using System.CommandLine;
using System.Diagnostics;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Helpers.Serialization;
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

rootCommand.SetHandler(async (context) =>
{
    var token = context.GetCancellationToken();
    context.ExitCode = await RootCommandHandler(
        context.ParseResult.GetValueForOption(processIdOption),
        context.ParseResult.GetValueForOption(fromOption)!,
        context.ParseResult.GetValueForOption(toOption)!,
        context.ParseResult.GetValueForOption(launcherConfiguration)!,
        context.GetCancellationToken());
});

return await rootCommand.InvokeAsync(args);


async static Task<int> RootCommandHandler(int processId, string from, string to, string launcherConfigurationBase64, CancellationToken cancellationToken)
{
    var launcherConfiguration = LauncherConfiguration.Deserialize(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(launcherConfigurationBase64)));
    launcherConfiguration.EnableDefaults();

    Log.Logger = new LoggerConfiguration()
        .AddLoggingConfiguration(launcherConfiguration, new[] { "fiskaltrust.LauncherUpdater", launcherConfiguration.CashboxId!.Value.ToString() })
        .Enrich.FromLogContext()
        .CreateLogger();

    cancellationToken.Register(() => Log.Warning("Shutdown requested."));

    try
    {
        await RunSelfUpdate(processId, from, to);
        return 0;
    }
    catch (Exception e)
    {
        Log.Error(e, "An exception occured.");
        return 1;
    }
    finally
    {
        Log.CloseAndFlush();
    }
}

async static Task RunSelfUpdate(int processId, string from, string to)
{
    var launcherProcess = Process.GetProcessById(processId);

    if (launcherProcess is not null)
    {
        Log.Information("Waiting for launcher to shut down.");
        await launcherProcess.WaitForExitAsync();
    }

    Log.Information("Copying launcher executable from \"{from}\" to \"{to}\".", from, to);
    File.Copy(from, to, true);

    Log.Information("Launcher update successful");
}
