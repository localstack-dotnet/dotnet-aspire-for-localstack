using System.Globalization;
using Amazon;
using Amazon.CloudFormation;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using LocalStack.Client;
using LocalStack.Client.Options;

namespace Aspire.Hosting.LocalStack.Internal;

/// <summary>
/// Internal helper class for configuring different types of resources to use LocalStack.
/// </summary>
internal static class LocalStackResourceConfigurator
{
    /// <summary>
    /// Configures a CloudFormation resource to use LocalStack endpoints.
    /// </summary>
    /// <param name="cloudFormationResource">The CloudFormation resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="state">The resolved LocalStack hosting state.</param>
    internal static void ConfigureCloudFormationResource(ICloudFormationTemplateResource cloudFormationResource, Uri localStackUrl, LocalStackHostingState state)
    {
        ArgumentNullException.ThrowIfNull(cloudFormationResource);
        ArgumentNullException.ThrowIfNull(localStackUrl);
        ArgumentNullException.ThrowIfNull(state);

        var sessionOptions = new SessionOptions(
            state.AccessKeyId,
            state.SecretAccessKey,
            state.SessionToken,
            state.Region);
        var configOptions = new ConfigOptions(
            localStackHost: localStackUrl.Host,
            useSsl: string.Equals(localStackUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            useLegacyPorts: state.UseLegacyPorts,
            edgePort: localStackUrl.Port);

        var session = SessionStandalone.Init()
            .WithSessionOptions(sessionOptions)
            .WithConfigurationOptions(configOptions)
            .Create();

        cloudFormationResource.CloudFormationClient = session.CreateClientByImplementation<AmazonCloudFormationClient>();
    }

    /// <summary>
    /// Pins the LocalStack region on the CDK stack's AWS SDK config so the asset uploader's STS and S3
    /// clients target the same region as the rest of LocalStack configuration. Credentials and endpoint
    /// are handled by <see cref="LocalStackCdkCredentialsOverride"/> and
    /// <see cref="LocalStackCdkAssetUploadEndpointCustomizer"/>.
    /// </summary>
    /// <param name="stackResource">The CDK stack resource to configure.</param>
    /// <param name="state">The resolved LocalStack hosting state.</param>
    internal static void ConfigureStackResource(IStackResource stackResource, LocalStackHostingState state)
    {
        ArgumentNullException.ThrowIfNull(stackResource);
        ArgumentNullException.ThrowIfNull(state);

        var regionName = string.IsNullOrEmpty(state.Region) ? "us-east-1" : state.Region;
        var region = RegionEndpoint.GetBySystemName(regionName);

        stackResource.AWSSDKConfig = new LocalStackAwsSdkConfig(region, stackResource.AWSSDKConfig?.SDKValidationEnabled ?? false);
    }

    /// <summary>
    /// Configures a project resource with LocalStack environment variables.
    /// </summary>
    /// <param name="projectResourceBuilder">The project resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="state">The resolved LocalStack hosting state.</param>
    internal static void ConfigureProjectResource(IResourceBuilder<IResourceWithEnvironment> projectResourceBuilder, Uri localStackUrl, LocalStackHostingState state)
    {
        ArgumentNullException.ThrowIfNull(projectResourceBuilder);
        ArgumentNullException.ThrowIfNull(localStackUrl);
        ArgumentNullException.ThrowIfNull(state);

        projectResourceBuilder.WithEnvironment(context =>
        {
            // Main LocalStack configuration
            context.EnvironmentVariables["LocalStack__UseLocalStack"] = state.Enabled.ToString();

            // Session configuration - AWS credentials and region
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKeyId"] = state.AccessKeyId;
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKey"] = state.SecretAccessKey;
            context.EnvironmentVariables["LocalStack__Session__AwsSessionToken"] = state.SessionToken;
            context.EnvironmentVariables["LocalStack__Session__RegionName"] = state.Region;

            // Config configuration - LocalStack connection settings
            context.EnvironmentVariables["LocalStack__Config__LocalStackHost"] = localStackUrl.Host;
            context.EnvironmentVariables["LocalStack__Config__UseSsl"] = state.UseSsl.ToString();
            context.EnvironmentVariables["LocalStack__Config__UseLegacyPorts"] = state.UseLegacyPorts.ToString();
            context.EnvironmentVariables["LocalStack__Config__EdgePort"] = localStackUrl.Port.ToString(CultureInfo.InvariantCulture);
        });
    }

    /// <summary>
    /// Configures an SQS Event Source resource with LocalStack environment variables.
    /// This enables AWS Lambda Tools to redirect AWS SDK calls to LocalStack for SQS event sources.
    /// </summary>
    /// <param name="resourceBuilder">The SQS Event Source resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="state">The resolved LocalStack hosting state.</param>
    internal static void ConfigureSqsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, LocalStackHostingState state)
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);
        ArgumentNullException.ThrowIfNull(localStackUrl);
        ArgumentNullException.ThrowIfNull(state);

        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["AWS_ENDPOINT_URL"] = localStackUrl.ToString();
            context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = state.AccessKeyId;
            context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = state.SecretAccessKey;
            context.EnvironmentVariables["AWS_SESSION_TOKEN"] = state.SessionToken;
            context.EnvironmentVariables["AWS_DEFAULT_REGION"] = state.Region;
        });
    }

    /// <summary>
    /// Configures a DynamoDB Streams event source resource with LocalStack environment variables.
    /// The helper is an external Lambda Test Tool process, so endpoint routing must use AWS SDK environment variables.
    /// </summary>
    /// <param name="resourceBuilder">The DynamoDB Streams event source resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="state">The resolved LocalStack hosting state.</param>
    internal static void ConfigureDynamoDbStreamsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, LocalStackHostingState state)
    {
        ArgumentNullException.ThrowIfNull(resourceBuilder);
        ArgumentNullException.ThrowIfNull(localStackUrl);
        ArgumentNullException.ThrowIfNull(state);

        resourceBuilder.WithEnvironment(context =>
        {
            var endpoint = localStackUrl.ToString();
            context.EnvironmentVariables["AWS_ENDPOINT_URL"] = endpoint;
            context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB"] = endpoint;
            context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"] = endpoint;
            context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = state.AccessKeyId;
            context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = state.SecretAccessKey;
            context.EnvironmentVariables["AWS_SESSION_TOKEN"] = state.SessionToken;
            context.EnvironmentVariables["AWS_DEFAULT_REGION"] = state.Region;
        });
    }
}
