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
using Xunit.Abstractions;

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
        [InlineData(Binding.Localhost, true, new[] { 0, 1, 2 })]
        [InlineData(Binding.Localhost, false, new[] { 4, 5, 6 })]
        [InlineData(Binding.Loopback, true, new[] { 7, 8, 9 })]
        [InlineData(Binding.Loopback, false, new[] { 10, 11, 12 })]
        [InlineData(Binding.Ip, true, new[] { 13, 14, 15 })]
        [InlineData(Binding.Ip, false, new[] { 16, 17, 18 })]
        [InlineData(Binding.Hostname, true, new[] { 19, 20, 21 })]
        [InlineData(Binding.Hostname, false, new[] { 22, 23, 24 })]
        public async Task PlebianScu_WithGrpcAndRestAndSoapShouldRespond(Binding binding, bool useHttpSysBinding, int[] ports)
        {
            Skip.If(!OperatingSystem.IsWindows() && useHttpSysBinding, "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && useHttpSysBinding && binding is not Binding.Localhost && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

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
                Url = new[] { $"grpc://{host}:{1500 + ports[0]}", $"rest://{host}:{1500 + ports[1]}", $"http://{host}:{1500 + ports[2]}" },
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
        [InlineData(Binding.Localhost, true, new[] { 25, 26, 27 })]
        [InlineData(Binding.Localhost, false, new[] { 28, 29, 30 })]
        [InlineData(Binding.Loopback, true, new[] { 31, 32, 33 })]
        [InlineData(Binding.Loopback, false, new[] { 34, 35, 36 })]
        [InlineData(Binding.Ip, true, new[] { 37, 38, 39 })]
        [InlineData(Binding.Ip, false, new[] { 40, 41, 42 })]
        [InlineData(Binding.Hostname, true, new[] { 43, 44, 45 })]
        [InlineData(Binding.Hostname, false, new[] { 46, 47, 48 })]
        public async Task PlebianQueue_WithGrpcAndRestAndSoapShouldRespond(Binding binding, bool useHttpSysBinding, int[] ports)
        {
            Skip.If(!OperatingSystem.IsWindows() && useHttpSysBinding, "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && useHttpSysBinding && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

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
                Url = new[] { $"grpc://{host}:{1500 + ports[0]}", $"rest://{host}:{1500 + ports[1]}", $"http://{host}:{1500 + ports[2]}" },
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
        [InlineData(Binding.Localhost, 49)]
        [InlineData(Binding.Loopback, 50)]
        [InlineData(Binding.Ip, 51)]
        [InlineData(Binding.Hostname, 52)]
        public async Task PlebianQueue_WithSamePort(Binding binding, int port)
        {
            Skip.If(!OperatingSystem.IsWindows(), "HttpSysBinding is only supported on windows");
            Skip.If(OperatingSystem.IsWindows() && !Runtime.IsAdministrator!.Value, $"Test needs to be run as an administrator with HttpSysBinding and {binding} binding");

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
                Url = new[] { $"rest://{host}:{1500 + port}/test1", $"rest://{host}:{1500 + port}/test2" },
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

        private async Task RunTest<T>(T instance, PackageType packageType, PackageConfiguration packageConfiguration, bool useHttpSysBinding, Func<Task> checks) where T : class
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
            var logger = Mock.Of<Serilog.ILogger>(MockBehavior.Strict);
            Mock.Get(logger).Setup(x => x.Information(It.IsAny<string>())).Verifiable();
            Mock.Get(monarch).Setup(x => x.Started()).Callback(() => started.SetResult());
            var processHostService = new ProcessHostService(new Dictionary<Guid, IProcessHostMonarch> { { packageConfiguration.Id, monarch } }, Mock.Of<Serilog.ILogger>());

            var services = new ServiceCollection();

            services.AddSingleton(_ => instance);

            var lifetime = new HostApplicationLifetime();

            var loggerHostingService = Mock.Of<ILogger<HostingService>>(MockBehavior.Strict);
            Mock.Get(loggerHostingService)
                .Setup(x => x.Log(
                    It.IsNotIn(new[] { LogLevel.Error, LogLevel.Critical }),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ))
                .Verifiable();

            var loggerProcessHostPlebian = Mock.Of<ILogger<ProcessHostPlebian>>(MockBehavior.Strict);
            Mock.Get(loggerProcessHostPlebian)
                .Setup(x => x.Log(
                    It.IsNotIn(new[] { LogLevel.Error, LogLevel.Critical }),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ))
                .Verifiable();

            var hostingService = new HostingService(loggerHostingService, packageConfiguration, launcherConfiguration, processHostService);
            using var plebian = new ProcessHostPlebian(loggerProcessHostPlebian, hostingService, launcherConfiguration, packageConfiguration, plebianConfiguration, services.BuildServiceProvider(), lifetime, processHostService);
            await plebian.StartAsync(new CancellationToken());

            if (started.Task != await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(10))))
            {
                throw new TimeoutException("plebian did not start in time");
            }

            await Task.Run(checks);
            Mock.Get(logger).Verify(x => x.Error(It.IsAny<string>()), Times.Never);
            Mock.Get(loggerHostingService).Verify();
            Mock.Get(loggerProcessHostPlebian).Verify();
        }

    }
}