#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.LocalStack;
using Aspire.Hosting.LocalStack.Annotations;
using Aspire.Hosting.LocalStack.CDK;
using Aspire.Hosting.LocalStack.Configuration;
using Aspire.Hosting.LocalStack.Container;
using LocalStack.Client.Contracts;
using LocalStack.Client.Options;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding LocalStack resources to the application model.
/// </summary>
public static class LocalStackResourceBuilderExtensions
{
    // Internal port is always 4566.
    private const int DefaultContainerPort = 4566;

    private const string CloudFormationReferenceAnnotation = "Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation";

    /// <summary>
    /// Configures all AWS resources in the application to use the specified LocalStack instance.
    /// Automatically detects CloudFormation templates and CDK stacks, and handles CDK bootstrap if needed.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="localStack">The LocalStack resource to connect all AWS resources to.</param>
    /// <returns>The distributed application builder.</returns>
    public static IDistributedApplicationBuilder UseLocalStack(this IDistributedApplicationBuilder builder, IResourceBuilder<ILocalStackResource>? localStack)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (localStack?.Resource.Options.UseLocalStack != true)
        {
            return builder;
        }

        // Check if we have any CDK stacks (IStackResource) - if so, we need CDK bootstrap
        var hasStackResources = builder.Resources
            .OfType<IStackResource>()
            .Any();

        IResourceBuilder<ICloudFormationTemplateResource>? cdkBootstrap = null;

        if (hasStackResources)
        {
            // Create a CDK bootstrap resource once for all CDK stacks
            cdkBootstrap = builder.AddAWSCDKBootstrapCloudFormationTemplate(excludeFromManifest: true);

            // Move the CDK bootstrap resource to be right after LocalStack for proper startup ordering
            var localStackIndex = builder.Resources.IndexOf(localStack.Resource);
            if (localStackIndex >= 0 && localStackIndex < builder.Resources.Count - 1)
            {
                var cdkBootstrapResource = cdkBootstrap.Resource;
                if (builder.Resources.Remove(cdkBootstrapResource))
                {
                    var insertIndex = Math.Min(localStackIndex + 1, builder.Resources.Count);
                    builder.Resources.Insert(insertIndex, cdkBootstrapResource);
                }
            }
        }

        foreach (var resource in builder.Resources)
        {
            if (resource.Annotations.Any(annotation => annotation is LocalStackEnabledAnnotation))
            {
                continue;
            }

            if (resource is ICloudFormationTemplateResource awsResource)
            {
                var awsResourceBuilder = builder.CreateResourceBuilder(awsResource);
                awsResourceBuilder.WithReference(localStack);

                if (awsResource is IStackResource && cdkBootstrap != null)
                {
                    awsResourceBuilder.WaitFor(cdkBootstrap);
                }
            }
            else if (resource.Annotations.Any(a =>
                         a is ResourceRelationshipAnnotation { Resource: ICloudFormationTemplateResource } rra
                         && rra.Resource.Annotations.Any(ra => string.Equals(ra.GetType().FullName, CloudFormationReferenceAnnotation, StringComparison.Ordinal)))
                     && resource is IResourceWithEnvironment and IResourceWithWaitSupport)
            {
                switch (resource)
                {
                    case ProjectResource pr:
                        builder.CreateResourceBuilder(pr).WithReference(localStack);
                        break;
                    case ExecutableResource er:
                        builder.CreateResourceBuilder(er).WithReference(localStack);
                        break;
                }
            }
        }

        return builder;
    }

    /// <summary>
    /// Adds a LocalStack container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="localStackOptions">The LocalStack configuration options. If null, default options will be used.</param>
    /// <param name="configureContainer">Optional action to configure container-specific options.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{LocalStackResource}"/>.</returns>
    public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
        this IDistributedApplicationBuilder builder,
        string name = "localstack",
        ILocalStackOptions? localStackOptions = null,
        IAWSSDKConfig? awsConfig = null,
        Action<LocalStackContainerOptions>? configureContainer = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var options = localStackOptions ?? builder.AddLocalStackOptions();

        if (!options.UseLocalStack)
        {
            return null;
        }

        if (awsConfig is { Region: not null })
        {
            options = options.WithRegion(awsConfig.Region.SystemName);
        }

        var containerOptions = new LocalStackContainerOptions();

        // Apply container configuration if provided
        configureContainer?.Invoke(containerOptions);

        var resource = new LocalStackResource(name, options);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(LocalStackContainerImageTags.Image)
            .WithImageRegistry(LocalStackContainerImageTags.Registry)
            .WithImageTag(LocalStackContainerImageTags.Tag)
            .WithEndpoint(
                port: options.Config.EdgePort,
                targetPort: DefaultContainerPort,
                scheme: "http",
                name: LocalStackResource.PrimaryEndpointName,
                isExternal: true)
            .WithHttpHealthCheck("/_localstack/health", 200, LocalStackResource.PrimaryEndpointName)
            .WithLifetime(containerOptions.Lifetime)
            .WithEnvironment("DEBUG", containerOptions.DebugLevel.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("LS_LOG", containerOptions.LogLevel.ToEnvironmentValue())
            .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
            .WithExternalHttpEndpoints()
            .ExcludeFromManifest(); // LocalStack is for local development only

        // Add any additional environment variables
        foreach (var (key, value) in containerOptions.AdditionalEnvironmentVariables)
        {
            resourceBuilder = resourceBuilder.WithEnvironment(key, value);
        }

        return resourceBuilder;
    }

    public static IResourceBuilder<ICloudFormationTemplateResource> AddAWSCDKBootstrapCloudFormationTemplate(
        this IDistributedApplicationBuilder builder,
        bool excludeFromManifest = false,
        string? templatePath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var cdkBootstrapTemplate = templatePath ?? CdkBootstrapManager.GetBootstrapTemplatePath();
        var addAwsCloudFormationTemplate = builder.AddAWSCloudFormationTemplate("CDKBootstrap", cdkBootstrapTemplate);

        if (excludeFromManifest)
        {
            addAwsCloudFormationTemplate.ExcludeFromManifest();
        }

        return addAwsCloudFormationTemplate;
    }

    /// <summary>
    /// Reads LocalStack configuration from the application's configuration system.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <returns>The configured LocalStack options from appsettings.json or default options if not configured.</returns>
    public static ILocalStackOptions AddLocalStackOptions(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Check if the LocalStack section is present in the configuration
        var localStackSection = builder.Configuration.GetSection("LocalStack");

        // If the section is not present, return default options
        if (!localStackSection.Exists())
        {
            return new LocalStackOptions().WithUseLocalStack(false);
        }

        // Create a new LocalStackOptions and bind the configuration
        var options = new LocalStackOptions();
        localStackSection.Bind(options, c => c.BindNonPublicProperties = true);

        return options;
    }
}
