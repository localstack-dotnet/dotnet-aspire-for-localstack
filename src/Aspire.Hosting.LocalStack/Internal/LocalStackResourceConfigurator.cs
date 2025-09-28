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
    /// Uses surgical annotation insertion to ensure proper timing in the annotation processing pipeline.
    /// </summary>
    /// <param name="resourceBuilder">The SQS Event Source resource to configure.</param>
    /// <param name="localStackUrl">The LocalStack URL.</param>
    /// <param name="options">The LocalStack configuration options.</param>
    internal static void ConfigureSqsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, ILocalStackOptions options)
    {
        var executableResource = resourceBuilder.Resource;

        // Create environment callback annotation for AWS_ENDPOINT_URL
        var localStackEnvCallback = new EnvironmentCallbackAnnotation(context =>
        {
            // Set AWS_ENDPOINT_URL to redirect AWS SDK calls to LocalStack
            // This is the standard AWS SDK environment variable that Lambda Tools respect
            context.EnvironmentVariables["AWS_ENDPOINT_URL"] = localStackUrl.ToString();
        });

        // Calculate the precise index for our new annotation based on priority rules
        var insertionIndex = GetInsertionIndex(executableResource.Annotations);

        // Perform surgical insert instead of just adding to the end
        executableResource.Annotations.Insert(insertionIndex, localStackEnvCallback);
    }

    /// <summary>
    /// Calculates the correct index to insert our environment annotation based on a prioritized list of rules.
    /// This ensures our environment is set before other critical lifecycle annotations are processed.
    /// </summary>
    /// <param name="annotations">The existing collection of annotations on the resource.</param>
    /// <returns>The calculated index for insertion.</returns>
    private static int GetInsertionIndex(ResourceAnnotationCollection annotations)
    {
        // Rule 1: Insert right before DcpInstancesAnnotation (commented out for now)
        // int index = annotations.ToList().FindIndex(a => a.GetType().Name == "DcpInstancesAnnotation");
        // if (index != -1) return index;

        // Rule 2: Insert after the first existing EnvironmentCallbackAnnotation
        var index = annotations.ToList().FindLastIndex(a => a is EnvironmentCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 3: Insert after CommandLineArgsCallbackAnnotation
        index = annotations.ToList().FindLastIndex(a => a is CommandLineArgsCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 4: Insert after ManifestPublishingCallbackAnnotation
        index = annotations.ToList().FindLastIndex(a => a is ManifestPublishingCallbackAnnotation);
        if (index != -1) return index + 1;

        // Rule 5: Insert after ResourceRelationshipAnnotation
        index = annotations.ToList().FindLastIndex(a => a is ResourceRelationshipAnnotation);
        if (index != -1) return index + 1;

        // Rule 6 (Fallback): If none of the above markers are found, insert at the beginning
        return 0;
    }
}
