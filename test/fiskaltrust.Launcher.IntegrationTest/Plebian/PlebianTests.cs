using FluentAssertions;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.ProcessHost;
using Moq;
using Microsoft.Extensions.Logging;
using fiskaltrust.Launcher.Configuration;
using Microsoft.Extensions.DependencyInjection;
using fiskaltrust.Launcher.IntegrationTest.Helpers;
using fiskaltrust.Middleware.Abstractions;
using fiskaltrust.ifPOS.v1;
using fiskaltrust.Launcher.Clients;
using fiskaltrust.ifPOS.v1.de;
using fiskaltrust.Launcher.Constants;
using System.Net;
using System.Diagnostics.Metrics;

namespace fiskaltrust.Launcher.IntegrationTest.Plebian
{
    public enum Binding
    {
        Localhost,
        Loopback,
        Ip,
        Hostname
    }

    public class PlebianTests
    {
        [SkippableTheory]
        [InlineData(Binding.Localhost, true)]
        [InlineData(Binding.Localhost, false)]
        [InlineData(Binding.Loopback, true)]
        [InlineData(Binding.Loopback, false)]
        [InlineData(Binding.Ip, true)]
        [InlineData(Binding.Ip, false)]
        [InlineData(Binding.Hostname, true)]
        [InlineData(Binding.Hostname, false)]
        public async Task PlebianScu_WithGrpcAndRestAndSoapShouldRespond(Binding binding, bool useHttpSysBinding)
        {
            Skip.If(!OperatingSystem.IsWindows() && useHttpSysBinding, "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && useHttpSysBinding && (binding is Binding.Ip or Binding.Hostname) && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

            string? host = binding switch
            {
                Binding.Localhost => "localhost",
                Binding.Loopback => "127.0.0.1",
                Binding.Ip => Dns.GetHostAddresses(Dns.GetHostName()).Where(i => i.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && i != IPAddress.Parse("127.0.0.1")).Select(i => i.ToString()).FirstOrDefault(),
                Binding.Hostname => Dns.GetHostName(),
                _ => throw new NotImplementedException(),
            };

            Skip.If(host is null, "Could not get host");

            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { $"grpc://{host}:1500", $"rest://{host}:1501", $"http://{host}:1502" },
                Version = "1.0.0"
            };

            await RunTest<IDESSCD>(new DummyDeSscd(), PackageType.SCU, packageConfiguration, useHttpSysBinding, async () =>
            {
                var grpcClient = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await grpcClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().Match<ScuDeEchoResponse>(r => r.Message == "test");

                var restClient = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await restClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().NotBeNull().And.Match<ScuDeEchoResponse>(r => r.Message == "test");

                var soapClientHttp = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[2],
                    UrlType = "http",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await soapClientHttp.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().NotBeNull().And.Match<ScuDeEchoResponse>(r => r.Message == "test");
            });
        }


        [SkippableTheory]
        [InlineData(Binding.Localhost, true)]
        [InlineData(Binding.Localhost, false)]
        [InlineData(Binding.Loopback, true)]
        [InlineData(Binding.Loopback, false)]
        [InlineData(Binding.Ip, true)]
        [InlineData(Binding.Ip, false)]
        [InlineData(Binding.Hostname, true)]
        [InlineData(Binding.Hostname, false)]
        public async Task PlebianQueue_WithGrpcAndRestAndSoapShouldRespond(Binding binding, bool useHttpSysBinding)
        {
            Skip.If(!OperatingSystem.IsWindows() && useHttpSysBinding, "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && useHttpSysBinding && (binding is Binding.Ip or Binding.Hostname) && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

            string? host = binding switch
            {
                Binding.Localhost => "localhost",
                Binding.Loopback => "127.0.0.1",
                Binding.Ip => Dns.GetHostAddresses(Dns.GetHostName()).Where(i => i.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && i != IPAddress.Parse("127.0.0.1")).Select(i => i.ToString()).FirstOrDefault(),
                Binding.Hostname => Dns.GetHostName(),
                _ => throw new NotImplementedException(),
            };

            Skip.If(host is null, "Could not get host");

            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { $"grpc://{host}:1503", $"rest://{host}:1504", $"http://{host}:1505" },
                Version = "1.0.0"
            };

            await RunTest<IPOS>(new DummyPos(), PackageType.Queue, packageConfiguration, useHttpSysBinding, async () =>
            {
                var grpcClient = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await grpcClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var restClient = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await restClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var soapClientHttp = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[2],
                    UrlType = "http",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await soapClientHttp.EchoAsync(new EchoRequest { Message = "test" })).Should().NotBeNull().And.Match<EchoResponse>(r => r.Message == "test");
            });
        }

        [SkippableTheory]
        [InlineData(Binding.Localhost)]
        [InlineData(Binding.Loopback)]
        [InlineData(Binding.Ip)]
        [InlineData(Binding.Hostname)]
        public async Task PlebianQueue_WithSamePort(Binding binding)
        {
            Skip.If(!OperatingSystem.IsWindows(), "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && (binding is Binding.Ip or Binding.Hostname) && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

            string? host = binding switch
            {
                Binding.Localhost => "localhost",
                Binding.Loopback => "127.0.0.1",
                Binding.Ip => Dns.GetHostAddresses(Dns.GetHostName()).Where(i => i.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && i != IPAddress.Parse("127.0.0.1")).Select(i => i.ToString()).FirstOrDefault(),
                Binding.Hostname => Dns.GetHostName(),
                _ => throw new NotImplementedException(),
            };

            Skip.If(host is null, "Could not get host");

            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { $"rest://{host}:1506/test1", $"rest://{host}:1506/test2" },
                Version = "1.0.0"
            };

            await RunTest<IPOS>(new DummyPos(), PackageType.Queue, packageConfiguration, true, async () =>
            {
                var restClient1 = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "rest",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await restClient1.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var restClient2 = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(10)
                });

                (await restClient2.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");
            });
        }

        private static async Task RunTest<T>(T instance, PackageType packageType, PackageConfiguration packageConfiguration, bool useHttpSysBinding, Func<Task> checks) where T : class
        {
            var launcherConfiguration = new LauncherConfiguration
            {
                CashboxId = Guid.NewGuid(),
                ServiceFolder = "TestService",
                LogLevel = LogLevel.Debug,
                UseHttpSysBinding = useHttpSysBinding
            };

            var plebianConfiguration = new PlebianConfiguration
            {
                PackageId = packageConfiguration.Id,
                PackageType = packageType
            };

            var started = new TaskCompletionSource();
            var monarch = Mock.Of<IProcessHostMonarch>();
            Mock.Get(monarch).Setup(x => x.Started()).Callback(() => started.SetResult());
            var processHostService = new ProcessHostService(new Dictionary<Guid, IProcessHostMonarch> { { packageConfiguration.Id, monarch } }, Mock.Of<Serilog.ILogger>());

            var services = new ServiceCollection();

            services.AddSingleton(_ => instance);

            var lifetime = new HostApplicationLifetime();

            var hostingService = new HostingService(Mock.Of<ILogger<HostingService>>(), packageConfiguration, launcherConfiguration, processHostService);
            using var plebian = new ProcessHostPlebian(Mock.Of<ILogger<ProcessHostPlebian>>(), hostingService, launcherConfiguration, packageConfiguration, plebianConfiguration, services.BuildServiceProvider(), lifetime, processHostService);

            await plebian.StartAsync(new CancellationToken());

            await started.Task;

            await checks();
        }
    }
}