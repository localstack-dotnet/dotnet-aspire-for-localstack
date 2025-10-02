using LocalStack.Client.Contracts;

namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Helper methods for LocalStack integration tests.
/// </summary>
internal static class LocalStackTestHelpers
{
    /// <summary>
    /// Gets a CloudFormation output value from a resource event.
    /// </summary>
    public static string? GetCloudFormationOutput(ResourceEvent resourceEvent, string outputName)
    {
        return resourceEvent.Snapshot.Properties
            .SingleOrDefault(snapshot => string.Equals(
                snapshot.Name,
                $"aws.cloudformation.output.{outputName}",
                StringComparison.Ordinal))?.Value as string;
    }

    /// <summary>
    /// Creates a LocalStack session from connection string.
    /// </summary>
    public static ISession CreateLocalStackSession(string connectionString, string regionName)
    {
        var connectionStringUri = new Uri(connectionString);
        var configOptions = new ConfigOptions(connectionStringUri.Host, edgePort: connectionStringUri.Port);
        var sessionOptions = new SessionOptions(regionName: regionName);

        return SessionStandalone.Init()
            .WithSessionOptions(sessionOptions)
            .WithConfigurationOptions(configOptions)
            .Create();
    }

    /// <summary>
    /// Waits for a CloudFormation stack resource to be running and extracts outputs.
    /// </summary>
    public static async Task<CloudFormationStackOutputs> WaitForStackOutputsAsync(
        ResourceNotificationService notificationService,
        string stackResourceName,
        CancellationToken cancellationToken)
    {
        await foreach (var resourceEvent in notificationService.WatchAsync(cancellationToken))
        {
            if (!string.Equals(stackResourceName, resourceEvent.Resource.Name, StringComparison.Ordinal))
            {
                continue;
            }

            if (resourceEvent.Snapshot.State?.Text is not { Length: > 0 } stateText ||
                !string.Equals(stateText, KnownResourceStates.Running, StringComparison.Ordinal))
            {
                continue;
            }

            return new CloudFormationStackOutputs(resourceEvent);
        }

        throw new TimeoutException($"Stack '{stackResourceName}' did not reach Running state");
    }
}
