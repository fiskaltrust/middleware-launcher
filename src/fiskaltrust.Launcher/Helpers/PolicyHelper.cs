using fiskaltrust.Launcher.Common.Configuration;
using Polly;
using Polly.Extensions.Http;

namespace fiskaltrust.Launcher.Helpers;

public static class PolicyHelper
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(LauncherConfiguration configuration)
    {
        return HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(configuration.DownloadRetry!.Value,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy(LauncherConfiguration configuration)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(configuration.DownloadTimeoutSec!.Value);
    }

}