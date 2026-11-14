using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackHealthRegistrationTests
{
    [Test]
    [Arguments("alpha-beta", "alphabeta")]
    [Arguments("prod-a", "prod-a1")]
    public async Task Distinct_Literal_Names_Do_Not_Collide(string first, string second)
    {
        var firstKey = LocalStackResourceBuilderExtensions.GetHealthCheckName(first);
        var secondKey = LocalStackResourceBuilderExtensions.GetHealthCheckName(second);

        await Assert.That(firstKey).IsEqualTo("localstack_health_" + first);
        await Assert.That(secondKey).IsEqualTo("localstack_health_" + second);
        await Assert.That(firstKey).IsNotEqualTo(secondKey);
    }

    [Test]
    public async Task Two_Resources_Resolve_HealthCheckService_And_Probe_Independent_Endpoints()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var first = builder.AddLocalStack("first", awsConfig: null, options => options.WithEnabled(true));
        var second = builder.AddLocalStack("second", awsConfig: null, options => options.WithEnabled(true));
        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        Allocate(first!.Resource, 12345);
        Allocate(second!.Resource, 12346);
        using var handler = new EndpointRoutingHandler();
        using var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Constants.LocalStackHealthClientName).Returns(client);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();

        var service = app.Services.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync(registration => registration.Tags.Contains("localstack"));
        var names = report.Entries.Keys.Order(StringComparer.Ordinal).ToArray();

        await Assert.That(names).IsEquivalentTo(["localstack_health_first", "localstack_health_second"]);
        await Assert.That(report.Entries["localstack_health_first"].Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(report.Entries["localstack_health_second"].Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(handler.Requests.ToArray()).IsEquivalentTo([
            "http://localhost:12345/_localstack/health", "http://localhost:12346/_localstack/health"]);
        var registrations = app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        foreach (var resource in new[] { first.Resource, second.Resource })
        {
            var key = resource.Annotations.OfType<HealthCheckAnnotation>().Single().Key;
            await Assert.That(key).IsEqualTo("localstack_health_" + resource.Name);
            await Assert.That(registrations.Count(registration => registration.Name == key)).IsEqualTo(1);
        }
    }

    private static void Allocate(ILocalStackResource resource, int port)
    {
        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().Single(annotation => annotation.Name == "http");
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.LocalhostNetwork,
            new AllocatedEndpoint(endpoint, "localhost", port, EndpointBindingMode.SingleAddress,
                targetPortExpression: null, networkId: KnownNetworkIdentifiers.LocalhostNetwork));
    }

    private sealed class EndpointRoutingHandler : HttpMessageHandler
    {
        internal ConcurrentQueue<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var uri = request.RequestUri ?? throw new InvalidOperationException("Health request has no URI.");
            Requests.Enqueue(uri.AbsoluteUri);
            var status = uri.AbsoluteUri switch
            {
                "http://localhost:12345/_localstack/health" => HttpStatusCode.ServiceUnavailable,
                "http://localhost:12346/_localstack/health" => HttpStatusCode.OK,
                _ => throw new InvalidOperationException("Health request used an unexpected endpoint."),
            };
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
