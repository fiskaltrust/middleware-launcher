﻿using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Download;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace fiskaltrust.Launcher.IntegrationTest.Download;

public class ConfigurationDownloaderTests
{
    [Fact]
    public async Task DownloadConfigurationAsync_ShouldRetryOnFailure()
    {
        var config = new LauncherConfiguration 
        { 
            UseOffline = false,
            DownloadRetry = 3,
            CashboxConfigurationFile = "config.json",
            ConfigurationUrl = new Uri("http://localhost:5000/"),
            CashboxId = Guid.NewGuid(),
            AccessToken = "test_token",
        };

        var messageHandlerMock = new Mock<HttpMessageHandler>();
        var callCount = 0;
        messageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    throw new HttpRequestException(); 
                }
                return new HttpResponseMessage(HttpStatusCode.OK); 
            });

        var httpClient = new HttpClient(messageHandlerMock.Object);

        var clientCurve = ECDiffieHellman.Create();
        var downloader = new ConfigurationDownloader(config, httpClient);

        var result = await downloader.DownloadConfigurationAsync(clientCurve);

        result.Should().BeTrue();  
        callCount.Should().Be(3); 
    }
}
