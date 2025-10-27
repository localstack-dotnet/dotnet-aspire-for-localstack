using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting.LocalStack.Internal;

internal sealed class LocalStackHealthCheck(Uri uri, string[] services) : IHealthCheck, IDisposable
{
    private readonly HttpClient _client =
        new(new SocketsHttpHandler { ActivityHeadersPropagator = null })
        {
            BaseAddress = uri, Timeout = TimeSpan.FromSeconds(1)
        };

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
#pragma warning disable CA2234
            using var response = await _client.GetAsync("_localstack/health", cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2234
            if (response.IsSuccessStatusCode)
            {
                var responseJson =
                    await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken).ConfigureAwait(false);
                var servicesNode = responseJson?["services"]?.AsObject();

                if (servicesNode is null)
                {
                    return HealthCheckResult.Unhealthy(
                        "LocalStack health response did not contain a 'services' object."
                    );
                }

                var failingServices = services
                    .Where(s =>
                        !servicesNode.ContainsKey(s)
                        || servicesNode[s]?.ToString() != "running"
                    )
                    .ToList();

                if (failingServices.Count == 0)
                {
                    return HealthCheckResult.Healthy("LocalStack is healthy.");
                }

                var reason =
                    $"The following required services are not running: {string.Join(", ", failingServices)}.";
                return HealthCheckResult.Unhealthy(
                    $"LocalStack is unhealthy. {reason}"
                );
            }

            return HealthCheckResult.Unhealthy("LocalStack is unhealthy.");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            return HealthCheckResult.Unhealthy("LocalStack is unhealthy.", ex);
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
