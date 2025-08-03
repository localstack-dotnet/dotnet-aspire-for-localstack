using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Annotation to track which resources are referencing a LocalStack instance.
/// This enables bidirectional relationship tracking for debugging and tooling support.
/// </summary>
/// <param name="targetResource">The name of the resource that is referencing LocalStack.</param>
[DebuggerDisplay("Type = {GetType().Name,nq}, TargetResource = {TargetResource}")]
internal sealed class LocalStackReferenceAnnotation(string targetResource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the name of the resource that is referencing LocalStack.
    /// </summary>
    public string TargetResource { get; } = targetResource ?? throw new ArgumentNullException(nameof(targetResource));
}
