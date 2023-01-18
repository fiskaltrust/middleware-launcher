using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using fiskaltrust.Launcher.ServiceInstallation;
using fiskaltrust.Launcher.Helpers;

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
        private readonly LauncherExecutablePath _launcherExecutablePath;

        public InstallCommandHandler(SubArguments subArguments, LauncherExecutablePath launcherExecutablePath)
        {
            _subArguments = subArguments;
            _launcherExecutablePath = launcherExecutablePath;
        }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

            var commandArgs = "run ";
            commandArgs += string.Join(" ", new[] {
                "--cashbox-id", _launcherConfiguration.CashboxId!.Value.ToString(),
                "--access-token", _launcherConfiguration.AccessToken!,
                "--sandbox", _launcherConfiguration.Sandbox!.Value.ToString(),
                "--launcher-configuration-file", LauncherConfigurationFile,
            }.Concat(_subArguments.Args));

            ServiceInstaller? installer = null;
            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(ServiceName ?? $"fiskaltrust-{_launcherConfiguration.CashboxId}", _launcherExecutablePath);
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(ServiceName ?? $"fiskaltrust-{_launcherConfiguration.CashboxId}", _launcherExecutablePath);
            }

            if (installer is not null)
            {
                return await installer.InstallService(commandArgs, ServiceDisplayName, DelayedStart).ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service installation see: {link}", ""); // TODO
            return 1;
        }
    }
}
