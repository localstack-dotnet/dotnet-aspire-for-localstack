using System.Globalization;
using Amazon.CloudFormation;
using Aspire.Hosting.ApplicationModel;
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
    internal static void ConfigureSqsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl)
    {
        resourceBuilder.WithEnvironment(context => context.EnvironmentVariables["AWS_ENDPOINT_URL"] = localStackUrl.ToString());
    }
}
