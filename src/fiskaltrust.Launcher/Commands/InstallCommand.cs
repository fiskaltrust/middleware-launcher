using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Extensions;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Serilog;
using ProtoBuf.Grpc.Server;
using fiskaltrust.Launcher.Download;
using System.Diagnostics;
using Serilog.Context;

namespace fiskaltrust.Launcher.Commands
{
    public class InstallCommand : CommonRunCommand
    {
        public InstallCommand() : base("install")
        {
            AddOption(new Option<string?>("--service-name"));
            AddOption(new Option<string?>("--display-name"));
            AddOption(new Option<bool>("--delayed-start"));
        }
    }

    public class InstallCommandHandler : CommonRunCommandHandler
    {
        public string? ServiceName { get; set; }
        public string? DisplayName { get; set; }
        public bool DelayedStart { get; set; }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

            if (!OperatingSystem.IsWindows())
            {
                Log.Error("For non windows service installation see: {link}", ""); // TODO
                return 1;
            }

            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    Log.Error("Run as admin to install service {link}", ""); // TODO
                    // return 1;
                }
            }

            _launcherConfiguration.DisableDefaults();

            _launcherConfiguration.CashboxConfigurationFile = MakeAbsolutePath(_launcherConfiguration.CashboxConfigurationFile);
            _launcherConfiguration.ServiceFolder = MakeAbsolutePath(_launcherConfiguration.ServiceFolder);
            _launcherConfiguration.LogFolder = MakeAbsolutePath(_launcherConfiguration.LogFolder);
            LauncherConfigurationFile = MakeAbsolutePath(LauncherConfigurationFile)!;

            _launcherConfiguration.EnableDefaults();

            var command = $"{Environment.ProcessPath ?? throw new Exception("Could not find launcher executable")} run --launcher-configuration-file {LauncherConfigurationFile}";

            command += typeof(LauncherConfiguration)
                .GetProperties()
                .Select(property =>
                {
                    var name = property.Name
                        .Select(c => char.IsUpper(c) ? $"-{c}".ToLower() : c.ToString());
                    var key = string.Concat(name).TrimStart('-');
                    var value = property.GetValue(_launcherConfiguration);

                    return (key, value);
                })
                .Aggregate("", (acc, s) => acc + (s.value != null ? $" --{s.key} \"{s.value}\"" : ""));

            var serviceName = ServiceName ?? $"fiskaltrust-{_launcherConfiguration.CashboxId}";

            var arguments = new List<string> {
                "create",
                $"\"{serviceName}\"",
                $"start={(DelayedStart ? "delayed-auto" : "auto")}",
                $"binPath=\"{command.Replace("\"", "\\\"")}\"",
                // $"depend=" // TODO
            };

            if(DisplayName != null)
            {
                arguments.Add($"DisplayName=\"{DisplayName}\"");
            }

            Log.Information("Installing service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", arguments))
            {
                Log.Information($"Could not install service \"{serviceName}\"");
                return 1;
            }

            Log.Information("Starting service.");
            if (!await RunProcess(@"C:\WINDOWS\system32\sc.exe", new[] { "start", $"\"{serviceName}\"" }))
            {
                Log.Warning($"Could not start service \"{serviceName}\"");
            } else {
                Log.Information($"successfully installed service \"{serviceName}\"");
            }

            return 0;
        }

        private static string? MakeAbsolutePath(string? path)
        {
            if(path != null)
            {
                return Path.GetFullPath(path);
            }

            return null;
        }

        private static async Task<bool> RunProcess(string fileName, IEnumerable<string> arguments)
        {
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = fileName;
            process.StartInfo.CreateNoWindow = false;

            process.StartInfo.Arguments = string.Join(" ", arguments);
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.EnableRaisingEvents = true;

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var withEnrichedContext = (Action log) => {
                var enrichedContext = LogContext.PushProperty("EnrichedContext", " sc.exe");
                log();
                enrichedContext.Dispose();
            };

            process.OutputDataReceived += (data, e) => withEnrichedContext(() => Log.Information(e.Data));
            process.ErrorDataReceived += (data, e) => withEnrichedContext(() => Log.Error(e.Data));

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
    }
}
