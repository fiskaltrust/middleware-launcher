using System.CommandLine;
using System.Diagnostics;
using System.Text;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Common.Extensions;
using Serilog;
using Serilog.Context;

var processIdOption = new Option<int>(name: "--launcher-process-id");
var launcherConfiguration = new Option<string>("--launcher-configuration");
var launcherConfigurationFile = new Option<string?>("--launcher-configuration-file");
var fromOption = new Option<string>(name: "--from");
var toOption = new Option<string>(name: "--to");

var rootCommand = new RootCommand("Updater for the fiskaltrust.Launcher")
{
  processIdOption,
  fromOption,
  toOption,
  launcherConfiguration,
  launcherConfigurationFile
};

rootCommand.SetHandler(async (context) =>
{
    var token = context.GetCancellationToken();
    context.ExitCode = await RootCommandHandler(
        context.ParseResult.GetValueForOption(processIdOption),
        context.ParseResult.GetValueForOption(fromOption)!,
        context.ParseResult.GetValueForOption(toOption)!,
        context.ParseResult.GetValueForOption(launcherConfiguration)!,
        context.ParseResult.GetValueForOption(launcherConfigurationFile),
        context.GetCancellationToken());
});

Console.OutputEncoding = Encoding.UTF8;

return await rootCommand.InvokeAsync(args);

async static Task<int> RootCommandHandler(int processId, string from, string to, string launcherConfigurationBase64, string? launcherConfigurationFile, CancellationToken cancellationToken)
{
    var launcherConfiguration = LauncherConfiguration.Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(launcherConfigurationBase64)));

    Log.Logger = new LoggerConfiguration()
        .AddLoggingConfiguration(launcherConfiguration)
        .AddFileLoggingConfiguration(launcherConfiguration, new[] { "fiskaltrust.LauncherUpdater", launcherConfiguration.CashboxId?.ToString() })
        .Enrich.FromLogContext()
        .CreateLogger();

    cancellationToken.Register(() => Log.Warning("Shutdown requested."));

    Log.Debug("Launcher Configuration: {@LauncherConfiguration}", launcherConfiguration.Redacted());

    try
    {
        await RunSelfUpdate(processId, from, to);

        Log.Information("Running launcher health check.");

        var process = new Process();
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.FileName = to;
        process.StartInfo.CreateNoWindow = false;

        process.StartInfo.Arguments = string.Join(" ", new string[] {
            "doctor",
            "--launcher-configuration", launcherConfigurationFile ?? "launcher.configuration.json",
        }) + LauncherConfigurationToArgs(launcherConfiguration);

        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;

        process.Start();

        var doctorTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(doctorTimeout.Token);
        var doctorCancelled = doctorTimeout.IsCancellationRequested;

        var withEnrichedContext = (Action log) =>
        {
            using var enrichedContext = LogContext.PushProperty("EnrichedContext", " Doctor");
            log();
        };

        var stdOut = await process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(stdOut))
        {
            withEnrichedContext(() => Log.Information("\n" + stdOut));
        }

        var stdErr = await process.StandardError.ReadToEndAsync(CancellationToken.None);
        if (!string.IsNullOrEmpty(stdErr))
        {
            withEnrichedContext(() => Log.Error("\n" + stdErr));
        }

        if (process.ExitCode != 0 || doctorCancelled)
        {
            Log.Error("Launcher healthcheck after update failed.");

            RollbackSelfUpdate(to);
            Log.Warning("Rolled back update."); ;

            return 1;
        }
        else
        {
            Log.Information("Launcher update successful");
            return 0;
        }
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

static string LauncherConfigurationToArgs(LauncherConfiguration launcherConfiguration)
{
    var result = "";
    foreach (var property in typeof(LauncherConfiguration).GetProperties())
    {
        var value = launcherConfiguration.Raw(l => property.GetValue(l));
        if (value is not null)
        {
            result += " -" + string.Concat(property.Name.Select(c => char.IsUpper(c) ? "-" + char.ToLower(c) : c.ToString()));
            result += " ";
            result += $"\"{value}\"";
        }
    }
    return result;
}

async static Task RunSelfUpdate(int processId, string from, string to)
{
    Process? launcherProcess = null;
    try
    {
        launcherProcess = Process.GetProcessById(processId);
    }
    catch { }

    if (launcherProcess is not null)
    {
        Log.Information("Waiting for launcher to shut down.");
        await launcherProcess.WaitForExitAsync();
    }

    Log.Information("Copying launcher executable from \"{from}\" to \"{to}\".", from, to);

    var backup = $"{to}.backup";
    var update = $"{to}.update";

    File.Move(from, update, true);
    File.Copy(to, backup, true);
    File.Move(update, to, true);
}

static void RollbackSelfUpdate(string to)
{
    var backup = $"{to}.backup";
    File.Copy(backup, to, true);
}