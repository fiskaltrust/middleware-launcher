using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Commands;
using System.CommandLine.Hosting;
using fiskaltrust.Launcher.Services;
using Serilog;
using fiskaltrust.Launcher.Extensions;

var command = new RootCommand {
  new RunCommand(),
  new HostCommand(),
};

await new CommandLineBuilder(command)
    .UseDefaults()
    .UseHost(host => host
      .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration.AddLoggingConfiguration())
      .UseConsoleLifetime()
      .ConfigureServices(services =>
      {
          services.AddSingleton<HostingService>();
      })
      .UseCommandHandler<HostCommand, HostCommandHandler>()
      .UseCommandHandler<RunCommand, RunCommandHandler>())
    .Build()
    .InvokeAsync(args);
