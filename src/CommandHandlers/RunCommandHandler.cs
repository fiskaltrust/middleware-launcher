using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using ProtoBuf.Grpc.Server;

namespace fiskaltrust.Launcher.CommandHandlers
{
    static class RunCommandHandlerFactory
    {
        public static ICommandHandler Create()
        {
            return CommandHandler.Create(Handle);
        }

        private static async Task Handle(HostingService hosting, LauncherConfiguration argsLauncherConfiguration, string launcherConfigurationFile, string cashboxConfigurationFile, CancellationToken cancellationToken)
        {
            var cashboxLauncherConfiguration = JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(cashboxConfigurationFile, cancellationToken))?.LauncherConfiguration;
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(launcherConfigurationFile, cancellationToken)) ?? new LauncherConfiguration();

            MergeLauncherConfiguration(cashboxLauncherConfiguration, launcherConfiguration);
            MergeLauncherConfiguration(argsLauncherConfiguration, launcherConfiguration);

            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(cashboxConfigurationFile, cancellationToken)) ?? throw new Exception("Empty Configuration File");

            var hosts = new Dictionary<Guid, ProcessHostMonarch>();
            var server = await hosting.HostService<IProcessHostService>(new Uri($"http://[::1]:{launcherConfiguration.LauncherPort ?? 0}"), HostingType.GRPC, new ProcessHostService(hosts));

            var uri = new Uri(server.Urls.First());
            // var server = new Server
            // {
            //     Ports = { new ServerPort("localhost", launcherConfiguration.LauncherPort ?? 0, ServerCredentials.Insecure) }
            // };
            // server.Services.AddCodeFirst(new ProcessHostService(hosts));
            // server.Start();


            // foreach (var helper in cashboxConfiguration.helpers)
            // {
            //     var host = new ProcessHostMonarch(uri, helper.Id, helper, PackageType.Helper);
            //     hosts.Add(helper.Id, host);
            //     await host.Start(cancellationToken);
            // }
            foreach (var scu in cashboxConfiguration.ftSignaturCreationDevices)
            {

                var host = new ProcessHostMonarch(uri, scu.Id, launcherConfiguration, scu, PackageType.SCU);
                hosts.Add(scu.Id, host);
                await host.Start(cancellationToken);
            }
            // foreach (var queue in cashboxConfiguration.ftQueues)
            // {
            //     var host = new ProcessHostMonarch(uri, queue.Id, queue, PackageType.Queue);
            //     hosts.Add(queue.Id, host);
            //     await host.Start(cancellationToken);
            // }

            await Task.WhenAll(hosts.Select(h => h.Value.Stopped()));
        }

        private static void MergeLauncherConfiguration(LauncherConfiguration? source, LauncherConfiguration? target)
        {
            if (source != null && target != null)
            {
                foreach (var property in typeof(LauncherConfiguration).GetProperties())
                {
                    var value = property.GetValue(source, null);

                    if (value != null)
                    {
                        property.SetValue(target, value, null);
                    }
                }
            }
        }
    }
}
