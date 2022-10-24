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
using fiskaltrust.Middleware.Interface.Client.Http;
using fiskaltrust.Middleware.Interface.Client;

namespace fiskaltrust.Launcher.IntegrationTest.Plebian
{
    public class PlebianTests
    {
        [Fact]
        public async Task PlebianScu_WithGrpcAndRestShouldRespond()
        {
            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { "grpc://localhost:1500", "rest://localhost:1501" },
                Version = "1.0.0"
            };

            var sscd = Mock.Of<IDESSCD>();

            Mock.Get(sscd).Setup(x => x.EchoAsync(It.IsAny<ScuDeEchoRequest>())).ReturnsAsync((ScuDeEchoRequest r) =>
            {
                return new ScuDeEchoResponse { Message = r.Message };
            });

            await RunTest(sscd, PackageType.SCU, packageConfiguration, async () =>
            {
                var grpcClient = new DESSCDClientFactory().CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = 0
                });

                (await grpcClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().Match<ScuDeEchoResponse>(r => r.Message == "test");

                var restClient = new DESSCDClientFactory().CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = 0,
                    Timeout = TimeSpan.FromSeconds(30)
                });

                (await restClient.EchoAsync(new ScuDeEchoRequest { Message = "test" })).Should().NotBeNull().And.Match<ScuDeEchoResponse>(r => r.Message == "test");
            });
        }

        [Fact]
        public async Task PlebianQueue_WithGrpcAndRestShouldRespond()
        {
            var packageConfiguration = new PackageConfiguration
            {
                Configuration = new(),
                Id = Guid.NewGuid(),
                Package = "test",
                Url = new[] { "grpc://localhost:1502", "rest://localhost:1503" },
                Version = "1.0.0"
            };

            var pos = Mock.Of<IPOS>();

            Mock.Get(pos).Setup(x => x.EchoAsync(It.IsAny<EchoRequest>())).ReturnsAsync((EchoRequest r) =>
            {
                return new EchoResponse { Message = r.Message };
            });

            await RunTest(pos, PackageType.Queue, packageConfiguration, async () =>
            {
                var grpcClient = new POSClientFactory().CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[0],
                    UrlType = "grpc",
                    RetryCount = null
                });

                (await grpcClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

                var restClient = new POSClientFactory().CreateClient(new ClientConfiguration
                {
                    Url = packageConfiguration.Url[1],
                    UrlType = "rest",
                    RetryCount = null
                });

                (await restClient.EchoAsync(new EchoRequest { Message = "test" })).Should().Match<EchoResponse>(r => r.Message == "test");

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

            launcherConfiguration.EnableDefaults();

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
            var plebian = new ProcessHostPlebian(Mock.Of<ILogger<ProcessHostPlebian>>(), hostingService, launcherConfiguration, packageConfiguration, plebianConfiguration, services.BuildServiceProvider(), lifetime, processHostService);

            await plebian.StartAsync(new CancellationToken());

            await started.Task;

            await checks();
        }
    }
}