using System.Net;
using AIChess.Core.Helpers;
using Google.Cloud.Iam.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using Policy = Polly.Policy;

namespace AIChess.Core.Models.HttpDelegates;

public class DelegateHandlerFactory
{

    public static DelegatingHandler GetDelegatingHandler<T>(ILoggerFactory output, bool thinking = false, int budget = 1024)
    {
        if (typeof(T) == typeof(LoggingHandler))
            return new LoggingHandler(new HttpClientHandler(), output);
        
        if (typeof(T) == typeof(OpenRouterReasoningHandler))
            return new OpenRouterReasoningHandler(new HttpClientHandler(), output);
        throw new NotSupportedException($"The type {typeof(T).Name} is not supported.");
    }
    public static HttpClient GetHttpClientWithHandler<T>(ILoggerFactory output, TimeSpan? timeOut = null, bool thinking = false, int budget = 1024)
    {
        var httpClientWithHandler = CreateResilientHttpClient(GetDelegatingHandler<T>(output, thinking, budget));
        if (timeOut.HasValue)
        {
            httpClientWithHandler.Timeout = timeOut.Value;
        }
        return httpClientWithHandler;
    }
    public static HttpClient CreateResilientHttpClient(DelegatingHandler delegatingHandler)
    {
        // 1. Retry policy: retry up to 3 times with exponential backoff when a transient error occurs
        //    or when a 429 (Too Many Requests) response is received.
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError() // handles HttpRequestException, HTTP 5xx and 408 responses
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
            );

        // 2. Per-attempt timeout: Each individual HTTP call is cancelled if it runs for more than 5 minutes.
        var attemptTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(5));

        // 3. Circuit breaker: If 5 consecutive calls fail, break for 20 minutes.
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(20)
            );

        // 4. Total request timeout: The overall HTTP call (including any retries) is cancelled after 30 minutes.
        var totalTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMinutes(30));

        // 5. Wrap the policies.
        //    The order here is important. In this case, the total timeout is outermost, then the retry policy,
        //    then the circuit breaker, and finally the per-attempt timeout is innermost.
        var policyWrap = Policy.WrapAsync(
            totalTimeoutPolicy,
            retryPolicy,
            circuitBreakerPolicy,
            attemptTimeoutPolicy
        );

        // 6. Create a handler that applies these policies.
        var policyHandler = new PolicyHttpMessageHandler(policyWrap)
        {
            InnerHandler = delegatingHandler
        };

        // 7. Finally, create and return the HttpClient instance.
        var client = new HttpClient(policyHandler);
        return client;
    }
}