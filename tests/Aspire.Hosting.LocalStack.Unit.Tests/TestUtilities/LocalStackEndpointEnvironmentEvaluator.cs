using System.Net.Sockets;

namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class LocalStackEndpointEnvironmentEvaluator
{
    internal static EndpointReference AddEndpoint(IResourceWithEndpoints resource, Uri localhost)
    {
        var annotation = new EndpointAnnotation(ProtocolType.Tcp, uriScheme: localhost.Scheme, name: "http", targetPort: 4566);
        resource.Annotations.Add(annotation);
        annotation.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.LocalhostNetwork,
            new AllocatedEndpoint(annotation, localhost.Host, localhost.Port, EndpointBindingMode.SingleAddress,
                targetPortExpression: null, networkId: KnownNetworkIdentifiers.LocalhostNetwork));
        annotation.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(annotation, "localstack-test", 4566, EndpointBindingMode.SingleAddress,
                targetPortExpression: null, networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
        return new EndpointReference(resource, "http");
    }

    internal static async Task<IReadOnlyDictionary<string, string>> EvaluateAsync(
        IResource resource, DistributedApplicationExecutionContext executionContext, IResource consumer,
        NetworkIdentifier networkIdentifier, CancellationToken cancellationToken = default)
    {
        var context = new EnvironmentCallbackContext(executionContext, resource, cancellationToken: cancellationToken);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolution = new ValueProviderContext { ExecutionContext = executionContext, Caller = consumer, Network = networkIdentifier };
        foreach (var (key, value) in context.EnvironmentVariables)
        {
            values[key] = value is IValueProvider provider
                ? await provider.GetValueAsync(resolution, cancellationToken) ?? string.Empty
                : (string)value;
        }

        return values;
    }
}
