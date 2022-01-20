using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;

var command = new RootCommand {
  new RunCommand(),
  new HostCommand() { IsHidden = true },
  new InstallCommand(),
  new UninstallCommand(),
};

command.Handler = System.CommandLine.Invocation.CommandHandler.Create(() =>
{
    Console.Error.WriteLine($"Please specify a command to run this application. Use '--help' for more information.");
});

var subArguments = new SubArguments(args.SkipWhile(a => a != "--").Skip(1));
args = args.TakeWhile(a => a != "--").ToArray();

await new CommandLineBuilder(command)
  .UseHost(host =>
  {
      if (WindowsServiceHelpers.IsWindowsService())
      {
          host.UseWindowsService();
      }
      else
      {
          host.UseConsoleLifetime();
      }

      host.ConfigureServices(services => services.AddSingleton(_ => subArguments));

      host
        .UseCommandHandler<HostCommand, HostCommandHandler>()
        .UseCommandHandler<RunCommand, RunCommandHandler>()
        .UseCommandHandler<InstallCommand, InstallCommandHandler>()
        .UseCommandHandler<UninstallCommand, UninstallCommandHandler>();
  })
  .UseHelp()
  .UseVersionOption()
  .Build().InvokeAsync(args);



