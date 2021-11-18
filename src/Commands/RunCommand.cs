using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using fiskaltrust.Launcher.Configuration;
using fiskaltrust.Launcher.Constants;
using fiskaltrust.Launcher.Interfaces;
using fiskaltrust.Launcher.ProcessHost;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;

namespace fiskaltrust.Launcher.Commands
{
    public class RunCommand : Command
    {
        public RunCommand() : base("run")
        {
            AddOption(new Option<string?>("--cashbox-id", getDefaultValue: () => null));
            AddOption(new Option<string?>("--access-token", getDefaultValue: () => null));
            AddOption(new Option<int?>("--launcher-port", getDefaultValue: () => null));
            AddOption(new Option<string?>("--service-folder", getDefaultValue: () => Paths.ServiceFolder));
            AddOption(new Option<string>("--launcher-configuration-file", getDefaultValue: () => "configuration.json"));
            AddOption(new Option<string>("--cashbox-configuration-file", getDefaultValue: () => "configuration.json"));
        }
    }

    public class RunCommandHandler : ICommandHandler
    {
        public LauncherConfiguration ArgsLauncherConfiguration { get; set; } = null!;
        public string LauncherConfigurationFile { get; set; } = null!;
        public string CashboxConfigurationFile { get; set; } = null!;
        

        private readonly ILoggerFactory _loggerFactory;
        private readonly HostingService _hosting;
        private readonly CancellationToken _cancellationToken;

        public RunCommandHandler()
        {
            // TODO create service collection, host ETC
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            var cashboxLauncherConfiguration = JsonSerializer.Deserialize<LauncherConfigurationInCashBoxConfiguration>(await File.ReadAllTextAsync(CashboxConfigurationFile, _cancellationToken))?.LauncherConfiguration;
            var launcherConfiguration = JsonSerializer.Deserialize<LauncherConfiguration>(await File.ReadAllTextAsync(LauncherConfigurationFile, _cancellationToken)) ?? new LauncherConfiguration();

            MergeLauncherConfiguration(cashboxLauncherConfiguration, launcherConfiguration);
            MergeLauncherConfiguration(ArgsLauncherConfiguration, launcherConfiguration);

            var cashboxConfiguration = JsonSerializer.Deserialize<ftCashBoxConfiguration>(await File.ReadAllTextAsync(CashboxConfigurationFile, _cancellationToken)) ?? throw new Exception("Empty Configuration File");

            var hosts = new Dictionary<Guid, ProcessHostMonarch>();
            var server = await _hosting.HostService<IProcessHostService>(new Uri($"http://[::1]:{launcherConfiguration.LauncherPort ?? 0}"), HostingType.GRPC, new ProcessHostService(hosts));

            var uri = new Uri(server.Urls.First());

            // foreach (var helper in cashboxConfiguration.helpers)
            // {
            //     var host = new ProcessHostMonarch(loggerFactory.CreateLogger<ProcessHostMonarch>(), uri, helper.Id, helper, PackageType.Helper);
            //     hosts.Add(helper.Id, host);
            //     await host.Start(cancellationToken);
            // }
            foreach (var scu in cashboxConfiguration.ftSignaturCreationDevices)
            {

                var host = new ProcessHostMonarch(_loggerFactory.CreateLogger<ProcessHostMonarch>(), uri, scu.Id, launcherConfiguration, scu, PackageType.SCU);
                hosts.Add(scu.Id, host);
                await host.Start(_cancellationToken);
            }
            // foreach (var queue in cashboxConfiguration.ftQueues)
            // {
            //     var host = new ProcessHostMonarch(loggerFactory.CreateLogger<ProcessHostMonarch>(), uri, queue.Id, queue, PackageType.Queue);
            //     hosts.Add(queue.Id, host);
            //     await host.Start(cancellationToken);
            // }

            await Task.WhenAll(hosts.Select(h => h.Value.Stopped()));

            return 0;
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
