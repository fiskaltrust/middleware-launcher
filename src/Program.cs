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
  new RunCommand() { Handler = new RunCommandHandler() },
  new HostCommand() { Handler = new HostCommandHandler() },
};

await command.InvokeAsync(args);
