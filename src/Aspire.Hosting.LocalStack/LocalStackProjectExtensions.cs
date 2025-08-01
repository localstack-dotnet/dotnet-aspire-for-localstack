#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring projects to automatically reference LocalStack resources.
/// </summary>
public static class LocalStackProjectExtensions
{
    /// <summary>
    /// Adds a reference to a LocalStack resource and automatically injects LocalStack configuration as environment variables.
    /// This enables LocalStack.Client.Extensions to automatically configure itself without manual setup in the service project.
    /// </summary>
    /// <typeparam name="TDestination">The project resource type.</typeparam>
    /// <param name="builder">The project resource builder.</param>
    /// <param name="localStackBuilder">The LocalStack resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{TDestination}"/>.</returns>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<ILocalStackResource>? localStackBuilder)
        where TDestination : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (localStackBuilder == null)
        {
            return builder;
        }

        builder.WaitFor(localStackBuilder);
        builder.WithReference(localStackBuilder, connectionName: localStackBuilder.Resource.Name);
        builder.WithAnnotation(new LocalStackEnabledAnnotation(localStackBuilder.Resource));
        if (!localStackBuilder.Resource.Annotations.Any(x =>
                x is LocalStackReferenceAnnotation referenceAnnotation
                && string.Equals(referenceAnnotation.TargetResource, builder.Resource.Name, StringComparison.Ordinal)))
        {
            localStackBuilder.WithAnnotation(new LocalStackReferenceAnnotation(builder.Resource.Name));
        }

        // Automatically inject LocalStack configuration as environment variables
        // This allows LocalStack.Client.Extensions to work without manual configuration in service projects
        return builder.WithEnvironment(context =>
        {
            var options = localStackBuilder.Resource.Options;

            // Main LocalStack configuration
            context.EnvironmentVariables["LocalStack__UseLocalStack"] = options.UseLocalStack.ToString();

            // Session configuration - AWS credentials and region
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKeyId"] = options.Session.AwsAccessKeyId;
            context.EnvironmentVariables["LocalStack__Session__AwsAccessKey"] = options.Session.AwsAccessKey;
            context.EnvironmentVariables["LocalStack__Session__AwsSessionToken"] = options.Session.AwsSessionToken;
            context.EnvironmentVariables["LocalStack__Session__RegionName"] = options.Session.RegionName;

            // Config configuration - LocalStack connection settings
            context.EnvironmentVariables["LocalStack__Config__LocalStackHost"] = options.Config.LocalStackHost;
            context.EnvironmentVariables["LocalStack__Config__UseSsl"] = options.Config.UseSsl.ToString();
            context.EnvironmentVariables["LocalStack__Config__UseLegacyPorts"] = options.Config.UseLegacyPorts.ToString();
            context.EnvironmentVariables["LocalStack__Config__EdgePort"] = options.Config.EdgePort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
    }
}
