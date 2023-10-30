using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.Helpers;

var command = new RootCommand {
    new RunCommand(),
    new HostCommand() { IsHidden = true },
    new InstallCommand(),
    new UninstallCommand(),
    new ConfigCommand(),
    new DoctorCommand(),
};

command.Handler = System.CommandLine.Invocation.CommandHandler.Create(() =>
{
    Console.Error.WriteLine($"Please specify a command to run this application. Use '--help' for more information.");
});

var subArguments = new SubArguments(args.SkipWhile(a => a != "--").Skip(1));
args = args.TakeWhile(a => a != "--").ToArray();

if (!args.Any())
{
    // If there are no arguments, add arguments for the "run" command
    args = new[] { "run" };
}

return await new CommandLineBuilder(command)
    .UseHost(host =>
    {
        host.UseCustomHostLifetime();

        host.ConfigureServices(services => services
            .Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(45))
            .AddSingleton(_ => subArguments)
            .AddSingleton(_ => new LauncherProcessId(Environment.ProcessId))
            .AddSingleton(_ => new LauncherExecutablePath
            {
                Path = Environment.ProcessPath ?? throw new Exception("Could not find launcher executable")
            })
            .AddSingleton<SelfUpdater>()
        );

        host
            .UseCommandHandler<HostCommand, HostCommandHandler>()
            .UseCommandHandler<RunCommand, RunCommandHandler>()
            .UseCommandHandler<InstallCommand, InstallCommandHandler>()
            .UseCommandHandler<UninstallCommand, UninstallCommandHandler>()
            .UseCommandHandler<ConfigGetCommand, ConfigGetCommandHandler>()
            .UseCommandHandler<ConfigSetCommand, ConfigSetCommandHandler>()
            .UseCommandHandler<DoctorCommand, DoctorCommandHandler>();
    })
    .UseHelp()
    .UseVersionOption()
    .Build().InvokeAsync(args);