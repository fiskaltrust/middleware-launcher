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

namespace fiskaltrust.Launcher.IntegrationTest.Plebian
{
    public class PlebianTests
    {
        [Fact]
        public async Task PlebianScu_WithGrpcAndRestAndSoapShouldRespond()
        {
            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { "grpc://localhost:1500", "rest://localhost:1501", "http://localhost:1502" },
                Version = "1.0.0"
            };

            await RunTest<IDESSCD>(new DummyDeSscd(), PackageType.SCU, packageConfiguration, async () =>
            {
                var grpcClient = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = 1
                });

                (await grpcClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().Match<ScuDeEchoResponse>(r => r.Message == "test");

                var restClient = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(30)
                });

                (await restClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().NotBeNull().And.Match<ScuDeEchoResponse>(r => r.Message == "test");

                var soapClientHttp = new DESSCDClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[2],
                    UrlType = "http",
                    RetryCount = 1,
                    Timeout = TimeSpan.FromSeconds(30)
                });

                (await soapClientHttp.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().NotBeNull().And.Match<ScuDeEchoResponse>(r => r.Message == "test");
            });
        }


        [Fact]
        public async Task PlebianQueue_WithGrpcAndRestAndSoapShouldRespond()
        {
            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { "grpc://localhost:1505", "rest://localhost:1506", "http://localhost:1507" },
                Version = "1.0.0"
            };

            await RunTest<IPOS>(new DummyPos(), PackageType.Queue, packageConfiguration, async () =>
            {
                var grpcClient = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = null
                });

                (await grpcClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var restClient = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = null
                });

                (await restClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var soapClientHttp = new POSClientFactory(new LauncherConfiguration()).CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[2],
                    UrlType = "http",
                    RetryCount = null,
                });

                (await soapClientHttp.EchoAsync(new EchoRequest { Message = "test" })).Should().NotBeNull().And.Match<EchoResponse>(r => r.Message == "test");
            });
        }

        private static async Task RunTest<T>(T instance, PackageType packageType, PackageConfiguration packageConfiguration, Func<Task> checks) where T : class
        {
            var launcherConfiguration = new LauncherConfiguration
            {
                CashboxId = Guid.NewGuid(),
                ServiceFolder = "TestService",
                LogLevel = LogLevel.Debug
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