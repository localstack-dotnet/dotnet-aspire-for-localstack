using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Annotation to track which resources are referencing a LocalStack instance.
/// This enables bidirectional relationship tracking for debugging and tooling support.
/// </summary>
/// <param name="resource">The resource that is referencing LocalStack.</param>
[DebuggerDisplay("Type = {GetType().Name,nq}, Resource = {Resource.Name,nq}")]
internal sealed class LocalStackReferenceAnnotation(IResource resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the resource that is referencing LocalStack.
    /// </summary>
    public IResource Resource { get; } = resource ?? throw new ArgumentNullException(nameof(resource));
}
