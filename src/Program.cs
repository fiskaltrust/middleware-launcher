using System.CommandLine;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Commands;
using System.CommandLine.Invocation;

var command = new RootCommand {
  new RunCommand() { Handler = CommandHandler.Create(typeof(RunCommandHandler).GetMethod(nameof(ICommandHandler.InvokeAsync))!)},
  new HostCommand() { Handler = CommandHandler.Create(typeof(HostCommandHandler).GetMethod(nameof(ICommandHandler.InvokeAsync))!)},
};

await command.InvokeAsync(args);
