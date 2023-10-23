﻿using fiskaltrust.Launcher.Common.Configuration;
using fiskaltrust.Launcher.Download;
using Polly;
using Polly.Extensions.Http;

namespace fiskaltrust.Launcher.Helpers;

public sealed class PolicyHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public PolicyHttpClient(LauncherConfiguration configuration)
    {

        var httpClientHandler = new HttpClientHandler { Proxy = ProxyFactory.CreateProxy(configuration.Proxy) };
        _httpClient = new HttpClient(httpClientHandler);
        _policy = GetPolicy(configuration);
    }

    public PolicyHttpClient(LauncherConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _policy = GetPolicy(configuration);
    }

    private IAsyncPolicy<HttpResponseMessage> GetPolicy(LauncherConfiguration configuration)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                configuration.DownloadRetry!.Value,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(configuration.DownloadTimeoutSec!.Value);

        return Policy.WrapAsync(retryPolicy, timeoutPolicy);
    }

    public Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> getMessage) => _policy.ExecuteAsync(ct => _httpClient.SendAsync(getMessage(), ct), CancellationToken.None);

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}