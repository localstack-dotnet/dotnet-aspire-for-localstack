using System.Collections.Immutable;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.LocalStack.Internal;

internal sealed class LocalStackHealthCheck(IHttpClientFactory httpClientFactory, Uri uri, ImmutableArray<string> services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient httpClient = httpClientFactory.CreateClient(Constants.LocalStackHealthClientName);
            using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy("LocalStack is unhealthy.");
            }

            if (services.Length == 0)
            {
                return HealthCheckResult.Healthy("LocalStack is healthy");
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken).ConfigureAwait(false);
            var servicesNode = responseJson?["services"]?.AsObject();

            if (servicesNode is null)
            {
                return HealthCheckResult.Unhealthy("LocalStack health response did not contain a 'services' object.");
            }

            var failingServices = services
                .Where(s =>
                {
                    var matchingKey = servicesNode
                        .FirstOrDefault(kvp => string.Equals(kvp.Key, s, StringComparison.OrdinalIgnoreCase))
                        .Key;

                    return matchingKey == null ||
                           !string.Equals(servicesNode[matchingKey]?.ToString(), "running", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (failingServices.Count == 0)
            {
                return HealthCheckResult.Healthy("LocalStack is healthy.");
            }

            var reason = $"The following required services are not running: {string.Join(',', failingServices)}";
            return HealthCheckResult.Unhealthy($"LocalStack is unhealthy. {reason}");
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy("LocalStack health check failed: network error", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return HealthCheckResult.Unhealthy("LocalStack health check timed out", ex);
        }
    }
}
