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

    public class InstallOptions
    {
        public InstallOptions(string? ServiceName, string? ServiceDisplayName, string? ServiceDescription, bool DelayedStart)
        {
            this.ServiceName = ServiceName;
            this.ServiceDisplayName = ServiceDisplayName;
            this.ServiceDescription = ServiceDescription;
            this.DelayedStart = DelayedStart;
        }

        public string? ServiceName { get; set; }
        public string? ServiceDisplayName { get; set; }
        public string? ServiceDescription { get; set; }
        public bool DelayedStart { get; set; }
    }

    public class InstallServices
    {
        public InstallServices(SubArguments subArguments, LauncherExecutablePath launcherExecutablePath)
        {
            SubArguments = subArguments;
            LauncherExecutablePath = launcherExecutablePath;
        }

        public readonly SubArguments SubArguments;
        public readonly LauncherExecutablePath LauncherExecutablePath;

    }

    public static class InstallHandler
    {
        public static async Task<int> HandleAsync(CommonOptions commonOptions, CommonProperties commonProperties, InstallOptions installOptions, InstallServices installServices)
        {
            var commandArgs = "run ";
            commandArgs += string.Join(" ", new[] {
                "--cashbox-id", commonProperties.LauncherConfiguration.CashboxId!.Value.ToString(),
                "--access-token", commonProperties.LauncherConfiguration.AccessToken!,
                "--sandbox", commonProperties.LauncherConfiguration.Sandbox!.Value.ToString(),
                "--launcher-configuration-file", $"\"{commonOptions.LauncherConfigurationFile}\"",
            }.Concat(installServices.SubArguments.Args));

            ServiceInstaller? installer = null;
            if (OperatingSystem.IsLinux())
            {
                installer = new LinuxSystemD(installOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", installServices.LauncherExecutablePath);
            }
            if (OperatingSystem.IsWindows())
            {
                installer = new WindowsService(installOptions.ServiceName ?? $"fiskaltrust-{commonProperties.LauncherConfiguration.CashboxId}", installServices.LauncherExecutablePath);
            }

            if (installer is not null)
            {
                return await installer.InstallService(commandArgs, installOptions.ServiceDisplayName, installOptions.DelayedStart).ConfigureAwait(false);
            }

            Log.Error("For non windows or linux(systemd) service installation see: {link}", ""); // TODO
            return 1;
        }
    }
}
