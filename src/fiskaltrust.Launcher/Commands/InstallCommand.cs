using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using fiskaltrust.Launcher.ServiceInstallation;

namespace fiskaltrust.Launcher.Commands
{
    public class InstallCommand : CommonCommand
    {
        public InstallCommand() : base("install")
        {
            AddOption(new Option<string?>("--service-name"));
            AddOption(new Option<string?>("--service-display-name"));
            AddOption(new Option<string?>("--service-description"));
            AddOption(new Option<bool>("--delayed-start"));
        }
    }

    public class InstallCommandHandler : CommonCommandHandler
    {
        public string? ServiceName { get; set; }
        public string? ServiceDisplayName { get; set; }
        public string? ServiceDescription { get; set; }
        public bool DelayedStart { get; set; }
        private readonly SubArguments _subArguments;

        public InstallCommandHandler(SubArguments subArguments)
        {
            _subArguments = subArguments;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

            LauncherConfiguration.DisableDefaults();

            LauncherConfiguration.CashboxConfigurationFile = MakeAbsolutePath(LauncherConfiguration.CashboxConfigurationFile);
            LauncherConfiguration.ServiceFolder = MakeAbsolutePath(LauncherConfiguration.ServiceFolder);
            LauncherConfiguration.LogFolder = MakeAbsolutePath(LauncherConfiguration.LogFolder);
            LauncherConfigurationFile = MakeAbsolutePath(LauncherConfigurationFile)!;

            LauncherConfiguration.EnableDefaults();

            var commandArgs = "run ";
            commandArgs += string.Join(" ", new[] {
                "--cashbox-id", LauncherConfiguration.CashboxId!.Value.ToString(),
                "--access-token", LauncherConfiguration.AccessToken!,
                "--sandbox", LauncherConfiguration.Sandbox!.Value.ToString(),
                "--launcher-configuration-file", LauncherConfigurationFile,
            }.Concat(_subArguments.Args));

            ServiceInstaller? installer = null;
            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(ServiceName ?? $"fiskaltrust-{LauncherConfiguration.CashboxId}");
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(ServiceName ?? $"fiskaltrust-{LauncherConfiguration.CashboxId}");
            }

            if (installer is not null)
            {
                return await installer.InstallService(commandArgs, ServiceDisplayName, DelayedStart).ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service installation see: {link}", ""); // TODO
            return 1;
        }

        private static string? MakeAbsolutePath(string? path)
        {
            if (path is not null)
            {
                return Path.GetFullPath(path);
            }

            return null;
        }
    }
}
