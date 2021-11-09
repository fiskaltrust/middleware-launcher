﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.CommandHandlers;
using Serilog;
using System.CommandLine.Hosting;

var command = new RootCommand { };

var run = new Command("run") {
  new Option<string?>("--cashbox-id", getDefaultValue: () => null),
  new Option<string?>("--access-token", getDefaultValue: () => null),
  new Option<int?>("--launcher-port", getDefaultValue: () => null),
  new Option<string>("--launcher-configuration-file", getDefaultValue: () => "configuration.json"),
  new Option<string>("--cashbox-configuration-file", getDefaultValue: () => "configuration.json"),
};
command.Add(run);

var host = new Command("host") {
  new Option<Guid>("--id"),
  new Option<string>("--package-config"),
  new Option<Uri?>("--monarch-uri"),
  new Option<PackageType>("--package-type"),
};
command.Add(host);

run.Handler = RunCommandHandlerFactory.Create();

host.Handler = HostCommandHandlerFactory.Create();

var parser = new CommandLineBuilder(command)
    .UseDefaults()
    .UseHost(host => host.ConfigureLogging(builder =>
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, shared: true)
                .CreateLogger();
            builder.AddSerilog(logger, dispose: true);
        }))
    .Build();

await parser.InvokeAsync(args);
