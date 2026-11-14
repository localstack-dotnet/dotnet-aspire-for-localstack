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
    /// Evaluates the <c>LocalStack__*</c> settings a resource receives, the way the orchestrator
    /// resolves them when it starts the resource.
    /// </summary>
    public static async Task<Dictionary<string, string>> GetLocalStackEnvironmentAsync(
        DistributedApplicationExecutionContext executionContext, IResource resource, CancellationToken cancellationToken)
    {
        var context = new EnvironmentCallbackContext(executionContext, resource);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context.EnvironmentVariables.Where(entry => entry.Key.StartsWith("LocalStack__", StringComparison.Ordinal)))
        {
            values[key] = value is IValueProvider provider
                ? await provider.GetValueAsync(cancellationToken) ?? string.Empty
                : (string)value;
        }

        return values;
    }

    /// <summary>
    /// Creates a LocalStack session from <c>LocalStack__*</c> settings, reading every key a
    /// LocalStack.Client consumer binds.
    /// </summary>
    public static ISession CreateConsumerSession(IReadOnlyDictionary<string, string> settings)
    {
        if (!bool.Parse(Required("LocalStack__UseLocalStack")))
        {
            throw new InvalidOperationException("The consumer settings do not select the LocalStack backend.");
        }

        return SessionStandalone.Init()
            .WithSessionOptions(new SessionOptions(
                Required("LocalStack__Session__AwsAccessKeyId"), Required("LocalStack__Session__AwsAccessKey"),
                Required("LocalStack__Session__AwsSessionToken"), Required("LocalStack__Session__RegionName")))
            .WithConfigurationOptions(new ConfigOptions(
                Required("LocalStack__Config__LocalStackHost"),
                bool.Parse(Required("LocalStack__Config__UseSsl")),
                bool.Parse(Required("LocalStack__Config__UseLegacyPorts")),
                int.Parse(Required("LocalStack__Config__EdgePort"), CultureInfo.InvariantCulture)))
            .Create();

        string Required(string key) => settings.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"The consumer settings have no {key}.");
    }

    /// <summary>
    /// Creates an AWS SQS client configured for LocalStack.
    /// </summary>
    public static IAmazonSQS CreateSqsClient(string connectionString, string regionName)
    {
        var session = CreateLocalStackSession(connectionString, regionName);
        return session.CreateClientByImplementation<AmazonSQSClient>();
    }

    /// <summary>
    /// Creates an AWS DynamoDB client configured for LocalStack.
    /// </summary>
    public static IAmazonDynamoDB CreateDynamoDbClient(string connectionString, string regionName)
    {
        var session = CreateLocalStackSession(connectionString, regionName);
        return session.CreateClientByImplementation<AmazonDynamoDBClient>();
    }

    /// <summary>
    /// Creates an AWS S3 client configured for LocalStack.
    /// </summary>
    public static IAmazonS3 CreateS3Client(string connectionString, string regionName)
    {
        var session = CreateLocalStackSession(connectionString, regionName);
        return session.CreateClientByImplementation<AmazonS3Client>();
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
