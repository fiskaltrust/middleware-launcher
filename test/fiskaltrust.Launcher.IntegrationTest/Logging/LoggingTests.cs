using FluentAssertions;
using fiskaltrust.Launcher.Logging;
using Serilog;
using fiskaltrust.Launcher.Services;
using fiskaltrust.storage.serialization.V0;
using fiskaltrust.Launcher.Common.Extensions;
using fiskaltrust.Launcher.Common.Configuration;
using System.Text.RegularExpressions;

namespace fiskaltrust.Launcher.IntegrationTest.Logging
{
    public class LoggingTests
    {
        [Fact]
        public void CollectionSink_Logs_SouldBeCollected()
        {
            var collectionSink = new CollectionSink();

            var logger = new LoggerConfiguration().WriteTo.Sink(collectionSink).CreateLogger();

            logger.Error("testError");
            logger.Warning("testWarning");
            logger.Information("testInformation");

            collectionSink.Events
                .Should()
                .HaveCount(3)
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testError")
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testWarning")
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testInformation");
        }

        [Fact]
        public void GrpcSink_SendLogs_SouldBeReceived()
        {
            var collectionSink = new CollectionSink();

            var processHostService = new ProcessHostService(new(), new LoggerConfiguration().WriteTo.Sink(collectionSink).CreateLogger());
            var packageConfiguration = new PackageConfiguration
            {
                Id = Guid.NewGuid(),
                Package = Guid.NewGuid().ToString()
            };

            var logger = new LoggerConfiguration().WriteTo.Sink(new GrpcSink(packageConfiguration, processHostService)).CreateLogger();

            logger.Error("testError");
            logger.Warning("testWarning");
            logger.Information("testInformation");

            collectionSink.Events
                .Should()
                .HaveCount(3)
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testError")
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testWarning")
                .And
                .Contain(e => e.MessageTemplate.ToString() == "testInformation");
        }

        [Fact]
        public void LoggerExtensions_Logs_ShouldBeLoggedToConsoleAndFiles()
        {
            if (Directory.Exists("TestLogs")) { Directory.Delete("TestLogs", true); }

            using StringWriter stdout = new();
            using StringWriter stderr = new();
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var launcherConfiguration = new LauncherConfiguration
            {
                LogFolder = "TestLogs",
                Sandbox = true
            };

            using (var logger = new LoggerConfiguration().AddLoggingConfiguration(launcherConfiguration).AddFileLoggingConfiguration(launcherConfiguration, new[] { "test" }).CreateLogger())
            {

                logger.Error("testError");
                logger.Information("testInformation");

                stdout.ToString().Should().MatchRegex("\\[[0-9]{2}:[0-9]{2}:[0-9]{2} INF\\] testInformation\r?\n");
                stderr.ToString().Should().MatchRegex("\\[[0-9]{2}:[0-9]{2}:[0-9]{2} ERR\\] testError\r?\n");
            }

            var file = Directory.EnumerateFiles("TestLogs").Should().HaveCount(1).And.Subject.First();
            file.Should().MatchRegex("log_test_[0-9]{8}.txt");

            var content = File.ReadAllText(file);
            content.Split('\n')
            .Should()
            .HaveCount(3)
            .And
            .Contain(l => new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{3} \\+[0-9]{2}:[0-9]{2} \\[ERR\\] testError\r?").IsMatch(l))
            .And
            .Contain(l => new Regex("[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{3} \\+[0-9]{2}:[0-9]{2} \\[INF\\] testInformation\r?").IsMatch(l));
        }
    }
}
