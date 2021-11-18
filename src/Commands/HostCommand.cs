using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Commands
{
    public class HostCommand : Command
    {
        public HostCommand() : base("host")
        {
            AddOption(new Option<Guid>("--id"));
            AddOption(new Option<string>("--package-config"));
            AddOption(new Option<Uri?>("--monarch-uri"));
            AddOption(new Option<PackageType>("--package-type"));
            AddOption(new Option<string>("--launcher-config"));
        }
    }

    public class HostCommandHandler : ICommandHandler
    {
        public Uri? MonarchUri { get; set; }
        public Guid Id { get; set; }
        public string PackageConfig { get; set; } = null!;
        public string LauncherConfig { get; set; } = null!;
        public PackageType PackageType { get; set; }

        private readonly ILoggerFactory _loggerFactory;
        private readonly HostingService _hosting;
        private readonly CancellationToken _cancellationToken;

        public HostCommandHandler(ILoggerFactory loggerFactory, HostingService hosting, IHostApplicationLifetime appLifetime)
        {
            _loggerFactory = loggerFactory;
            _hosting = hosting;
            _cancellationToken = appLifetime.ApplicationStopping;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var host = new ProcessHostPlebian(
                _loggerFactory.CreateLogger<ProcessHostPlebian>(),
                _hosting,
                MonarchUri,
                Id,
                JsonSerializer.Deserialize<LauncherConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(LauncherConfig))) ?? throw new Exception($"Could not deserialize {nameof(LauncherConfig)}"),
                JsonSerializer.Deserialize<PackageConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(PackageConfig))) ?? throw new Exception($"Could not deserialize {nameof(PackageConfig)}"),
                PackageType);

            await host.Run(_cancellationToken);

            return 0;
        }
    }
}
