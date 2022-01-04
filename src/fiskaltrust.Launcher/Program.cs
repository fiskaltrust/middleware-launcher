using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;

var command = new RootCommand {
  new RunCommand(),
  new HostCommand() { IsHidden = true },
};

command.AddValidator(result => "Must specify command.");

command.Handler = System.CommandLine.Invocation.CommandHandler.Create(() =>
{
    Console.Error.WriteLine("Must specify command.");
});

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

      host
        .UseCommandHandler<HostCommand, HostCommandHandler>()
        .UseCommandHandler<RunCommand, RunCommandHandler>();
  })
  .UseHelp()
  .UseVersionOption()
  .Build().InvokeAsync(args);



