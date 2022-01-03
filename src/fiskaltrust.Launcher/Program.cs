using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;

var command = new RootCommand {
  new RunCommand(),
  new HostCommand(),
};


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
  .Build().InvokeAsync(args);



