using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.LocalStack.Annotations;

namespace Aspire.Hosting.LocalStack.Internal;

/// <summary>
/// Factory for creating LocalStack connection string callbacks with proper configuration logic.
/// </summary>
internal static class LocalStackConnectionStringAvailableCallback
{
    /// <summary>
    /// Creates a callback that configures referenced resources when the LocalStack connection string becomes available.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <returns>A callback function for LocalStack connection string availability.</returns>
    internal static Func<ILocalStackResource, ConnectionStringAvailableEvent, CancellationToken, Task> CreateCallback(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return async (localStackResource, _, ct) =>
        {
            var localStackOptions = localStackResource.Options;

            if (!localStackOptions.UseLocalStack)
            {
                return;
            }

            var connectionString = await localStackResource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{localStackResource.Name}' resource but the connection string was null.");
            }

            var localStackUrl = new Uri(connectionString);

            var resourceBuilder = builder.CreateResourceBuilder(localStackResource);
            resourceBuilder.WithEnvironment("LOCALSTACK_HOST", $"{localStackUrl.Host}:{localStackUrl.Port.ToString(CultureInfo.InvariantCulture)}");

            var referencedResources = localStackResource.Annotations.OfType<LocalStackReferenceAnnotation>();
            foreach (var resource in referencedResources.Select(annotation => annotation.Resource))
            {
                var hasLocalStackEnabledAnnotation = resource.HasAnnotationOfType<LocalStackEnabledAnnotation>();

                if (!hasLocalStackEnabledAnnotation)
                {
                    continue;
                }

                if (resource is ICloudFormationTemplateResource cft)
                {
                    LocalStackResourceConfigurator.ConfigureCloudFormationResource(cft, localStackUrl, localStackOptions);
                }
                else if (resource is ExecutableResource er &&
                         string.Equals(er.GetType().FullName, Constants.SQSEventSourceResource, StringComparison.Ordinal))
                {
                    var executableResourceBuilder = builder.CreateResourceBuilder(er);
                    LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(executableResourceBuilder, localStackUrl);
                }
                else if (resource.Annotations.Any(a =>
                             a is ResourceRelationshipAnnotation { Resource: ICloudFormationTemplateResource } rra
                             && rra.Resource.Annotations.Any(ra => string.Equals(ra.GetType().FullName, Constants.CloudFormationReferenceAnnotation, StringComparison.Ordinal)))
                         && resource is IResourceWithEnvironment resourceWithEnvironment and IResourceWithWaitSupport)
                {
                    var projectResourceBuilder = builder.CreateResourceBuilder(resourceWithEnvironment);

                    LocalStackResourceConfigurator.ConfigureProjectResource(projectResourceBuilder, localStackUrl, localStackOptions);
                }
            }
        };
    }
}
