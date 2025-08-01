using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Annotation to mark that a resource has been configured to use LocalStack.
/// This enables tooling and debugging support to understand LocalStack dependencies.
/// </summary>
/// <param name="localStackResource">The LocalStack resource this AWS resource depends on.</param>
internal sealed class LocalStackEnabledAnnotation(ILocalStackResource localStackResource) : IResourceAnnotation
{
    /// <summary>
    /// The LocalStack resource that this AWS resource is configured to use.
    /// </summary>
    public ILocalStackResource LocalStackResource { get; } = localStackResource;
}
