#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack;
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

    /// <summary>
    /// Adds a LocalStack container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="options">The LocalStack configuration options. If null, default options will be used.</param>
    /// <param name="configureContainer">Optional action to configure container-specific options.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{LocalStackResource}"/>.</returns>
    public static IResourceBuilder<LocalStackResource> AddLocalStack(
        this IDistributedApplicationBuilder builder,
        string name,
        ILocalStackOptions? options = null,
        Action<LocalStackContainerOptions>? configureContainer = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var localStackOptions = options ?? new LocalStackOptions();
        var containerOptions = new LocalStackContainerOptions();

        if (!localStackOptions.UseLocalStack)
        {
            // Return
        }

        // Apply container configuration if provided
        configureContainer?.Invoke(containerOptions);

        var resource = new LocalStackResource(name, localStackOptions);

        var resourceBuilder = builder.AddResource(resource)
            .WithImage(LocalStackContainerImageTags.Image)
            .WithImageRegistry(LocalStackContainerImageTags.Registry)
            .WithImageTag(LocalStackContainerImageTags.Tag)
            .WithEndpoint(
                port: localStackOptions.Config.EdgePort,
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

    /// <summary>
    /// Reads LocalStack configuration from the application's configuration system.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <returns>The configured LocalStack options from appsettings.json or default options if not configured.</returns>
    public static ILocalStackOptions AddLocalStackConfig(this IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Check if the LocalStack section is present in the configuration
        var localStackSection = builder.Configuration.GetSection("LocalStack");

        // If the section is not present, return default options
        if (!localStackSection.Exists())
        {
            return new LocalStackOptions();
        }

        // Create a new LocalStackOptions and bind the configuration
        var options = new LocalStackOptions();
        localStackSection.Bind(options, c => c.BindNonPublicProperties = true);

        return options;
    }
}
