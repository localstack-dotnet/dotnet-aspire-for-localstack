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

        if (localStackBuilder?.Resource.Options.UseLocalStack != true)
        {
            return builder;
        }

        builder.WaitFor(localStackBuilder);
        builder.WithReference(localStackBuilder, connectionName: localStackBuilder.Resource.Name);
        builder.WithAnnotation(new LocalStackEnabledAnnotation(localStackBuilder.Resource));

        // Add bidirectional reference annotation if not already present
        if (!localStackBuilder.Resource.Annotations.Any(x =>
                x is LocalStackReferenceAnnotation referenceAnnotation
                && ReferenceEquals(referenceAnnotation.Resource, builder.Resource)))
        {
            localStackBuilder.WithAnnotation(new LocalStackReferenceAnnotation(builder.Resource));
        }

        return builder;
    }
}
