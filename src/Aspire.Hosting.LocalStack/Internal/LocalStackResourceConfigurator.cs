using System.Globalization;
using Amazon;
using Amazon.CloudFormation;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using LocalStack.Client;
using LocalStack.Client.Contracts;
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
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureCloudFormationResource(ICloudFormationTemplateResource cloudFormationResource, Uri localStackUrl, ILocalStackOptions options)
    {
        var configOptions = new ConfigOptions(
            localStackHost: localStackUrl.Host,
            useSsl: options.Config.UseSsl,
            useLegacyPorts: options.Config.UseLegacyPorts,
            edgePort: localStackUrl.Port);

        var session = SessionStandalone.Init()
            .WithSessionOptions(options.Session)
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
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureStackResource(IStackResource stackResource, ILocalStackOptions options)
    {
        ArgumentNullException.ThrowIfNull(stackResource);
        ArgumentNullException.ThrowIfNull(options);

        var regionName = string.IsNullOrEmpty(options.Session.RegionName) ? "us-east-1" : options.Session.RegionName;
        var region = RegionEndpoint.GetBySystemName(regionName);

        stackResource.AWSSDKConfig = new LocalStackAwsSdkConfig(region, stackResource.AWSSDKConfig?.SDKValidationEnabled ?? false);
    }

    /// <summary>
    /// Configures a project resource with LocalStack environment variables.
    /// </summary>
    /// <param name="projectResourceBuilder">The project resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureProjectResource(IResourceBuilder<IResourceWithEnvironment> projectResourceBuilder, Uri localStackUrl, ILocalStackOptions options)
    {
        projectResourceBuilder.WithEnvironment(context =>
        {
            // Main LocalStack configuration
            context.EnvironmentVariables["LocalStack__UseLocalStack"] = options.UseLocalStack.ToString();

            // Session configuration - AWS credentials and region
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKeyId"] = options.Session.AwsAccessKeyId;
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKey"] = options.Session.AwsAccessKey;
            context.EnvironmentVariables["LocalStack__Session__AwsSessionToken"] = options.Session.AwsSessionToken;
            context.EnvironmentVariables["LocalStack__Session__RegionName"] = options.Session.RegionName;

            // Config configuration - LocalStack connection settings
            context.EnvironmentVariables["LocalStack__Config__LocalStackHost"] = localStackUrl.Host;
            context.EnvironmentVariables["LocalStack__Config__UseSsl"] = options.Config.UseSsl.ToString();
            context.EnvironmentVariables["LocalStack__Config__UseLegacyPorts"] = options.Config.UseLegacyPorts.ToString();
            context.EnvironmentVariables["LocalStack__Config__EdgePort"] = localStackUrl.Port.ToString(CultureInfo.InvariantCulture);
        });
    }

    /// <summary>
    /// Configures an SQS Event Source resource with LocalStack environment variables.
    /// This enables AWS Lambda Tools to redirect AWS SDK calls to LocalStack for SQS event sources.
    /// </summary>
    /// <param name="resourceBuilder">The SQS Event Source resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureSqsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, ILocalStackOptions options)
    {
        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["AWS_ENDPOINT_URL"] = localStackUrl.ToString();
            context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = options.Session.AwsAccessKeyId;
            context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = options.Session.AwsAccessKey;
            context.EnvironmentVariables["AWS_SESSION_TOKEN"] = options.Session.AwsSessionToken;
            context.EnvironmentVariables["AWS_DEFAULT_REGION"] = options.Session.RegionName;
        });
    }

    /// <summary>
    /// Configures a DynamoDB Streams event source resource with LocalStack environment variables.
    /// The helper is an external Lambda Test Tool process, so endpoint routing must use AWS SDK environment variables.
    /// </summary>
    /// <param name="resourceBuilder">The DynamoDB Streams event source resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureDynamoDbStreamsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, ILocalStackOptions options)
    {
        resourceBuilder.WithEnvironment(context =>
        {
            var endpoint = localStackUrl.ToString();
            context.EnvironmentVariables["AWS_ENDPOINT_URL"] = endpoint;

            // A pre-existing service-specific endpoint wins over LocalStack: the AWS integration wires these
            // to its DynamoDB Local container when the Lambda references one, and users may override them
            // explicitly. Both callbacks run before this one, so present keys mean LocalStack must defer.
            if (!context.EnvironmentVariables.ContainsKey("AWS_ENDPOINT_URL_DYNAMODB"))
            {
                context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB"] = endpoint;
            }

            if (!context.EnvironmentVariables.ContainsKey("AWS_ENDPOINT_URL_DYNAMODB_STREAMS"))
            {
                context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"] = endpoint;
            }

            context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = options.Session.AwsAccessKeyId;
            context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = options.Session.AwsAccessKey;
            context.EnvironmentVariables["AWS_SESSION_TOKEN"] = options.Session.AwsSessionToken;
            // The .NET SDK resolves region from AWS_REGION; AWS_DEFAULT_REGION is kept for CLI-convention compatibility.
            context.EnvironmentVariables["AWS_REGION"] = options.Session.RegionName;
            context.EnvironmentVariables["AWS_DEFAULT_REGION"] = options.Session.RegionName;
        });
    }
}
