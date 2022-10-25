using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using fiskaltrust.Launcher.ServiceInstallation;

namespace fiskaltrust.Launcher.Commands
{
    public class UninstallCommand : CommonCommand
    {
        public UninstallCommand() : base("uninstall")
        {
            AddOption(new Option<string?>("--service-name"));
        }
    }

    public class UninstallCommandHandler : CommonCommandHandler
    {
        public string? ServiceName { get; set; }

        public new async Task<int> InvokeAsync(InvocationContext context)
        {
            if (await base.InvokeAsync(context) != 0)
            {
                return 1;
            }

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
                return await installer.UninstallService().ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service uninstallation see: {link}", ""); // TODO
            return 1;
        }
    }
}
