using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.CommandHandlers
{
    static class HostCommandHandlerFactory // TODO Create abstraction over commands? (use like this https://github.com/dotnet/command-line-api/pull/671)
    {
        public static ICommandHandler Create()
        {
            return CommandHandler.Create(Handle);
        }

        private static async Task Handle(Uri? monarchUri, Guid id, string packageConfig, PackageType packageType, CancellationToken cancellationToken)
        {
            var host = new ProcessHostPlebian(monarchUri, id, JsonSerializer.Deserialize<PackageConfiguration>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(packageConfig))) ?? throw new ArgumentNullException(nameof(packageConfig)), packageType);

            await host.Run(cancellationToken);
        }
    }
}
