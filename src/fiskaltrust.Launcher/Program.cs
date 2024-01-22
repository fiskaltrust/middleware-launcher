using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;
using System.CommandLine.NamingConventionBinder;

var runCommand = new RunCommand()
{
    Handler = CommandHandler.Create<CommonOptions, RunOptions, IHost>(
            (commonOptions, runOptions, host) => CommonHandler.HandleAsync<RunOptions, RunServices>(commonOptions, runOptions, host, RunHandler.HandleAsync))
};

var command = new RootCommand("Launcher for the fiskaltrust.Middleware") {
    runCommand,
    new HostCommand() {
        IsHidden = true,
        Handler = CommandHandler.Create<fiskaltrust.Launcher.Commands.HostOptions, IHost>(
            (hostOptions, host) => HostHandler.HandleAsync(hostOptions, host.Services.GetRequiredService<HostServices>()))
    },
    new InstallCommand() {
        Handler = CommandHandler.Create<CommonOptions, InstallOptions, IHost>(
            (commonOptions, installOptions, host) => CommonHandler.HandleAsync<InstallOptions, InstallServices>(commonOptions, installOptions, host, InstallHandler.HandleAsync))
    },
    new UninstallCommand() {
        Handler = CommandHandler.Create<CommonOptions, UninstallOptions, IHost>(
            (commonOptions, uninstallOptions, host) => CommonHandler.HandleAsync<UninstallOptions, UninstallServices>(commonOptions, uninstallOptions, host, UninstallHandler.HandleAsync))
    },
    new ConfigCommand(),
    new DoctorCommand() {
        Handler = CommandHandler.Create<CommonOptions, DoctorOptions, IHost>(
            (commonOptions, doctorOptions, host) => CommonHandler.HandleAsync<DoctorOptions, DoctorServices>(commonOptions, doctorOptions, host, DoctorHandler.HandleAsync))
    },
};

if (!args.Any())
{
    args = new[] { runCommand.Name };
}

var subArguments = new SubArguments(args.SkipWhile(a => a != "--").Skip(1));
args = args.TakeWhile(a => a != "--").ToArray();

return await new CommandLineBuilder(command)
  .UseHost(
    host =>
      {
          host.UseCustomHostLifetime();

          host.ConfigureServices(services => services
            .Configure<Microsoft.Extensions.Hosting.HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(45))
            .AddSingleton(_ => subArguments)
            .AddSingleton(_ => new LauncherProcessId(Environment.ProcessId))
            .AddSingleton(_ => new LauncherExecutablePath
            {
                Path = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable")
            })
            .AddSingleton<SelfUpdater>()
            .AddSingleton<RunServices>()
            .AddSingleton<HostServices>()
            .AddSingleton<InstallServices>()
            .AddSingleton<UninstallServices>()
            .AddSingleton<DoctorServices>()
          );
      })
  .UseHelp()
  .UseVersionOption()
  .Build()
  .InvokeAsync(args);
