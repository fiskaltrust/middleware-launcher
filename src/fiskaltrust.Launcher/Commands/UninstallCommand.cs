using System.CommandLine;
using System.CommandLine.Invocation;
using Serilog;
using System.Diagnostics;
using Serilog.Context;
using fiskaltrust.Launcher.ServceInstallation;

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

            if (OperatingSystem.IsLinux())
            {
                var linuxSystemd = new LinuxSystemD(ServiceName);
                return linuxSystemd.UninstallSystemD();
            }

            if (OperatingSystem.IsWindows())
            {
                var windowsService = new WindowsService(ServiceName ?? $"fiskaltrust-{_launcherConfiguration.CashboxId}");
                return await windowsService.UninstallService().ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service uninstallation see: {link}", ""); // TODO
            return 1;
        }
    }
}
