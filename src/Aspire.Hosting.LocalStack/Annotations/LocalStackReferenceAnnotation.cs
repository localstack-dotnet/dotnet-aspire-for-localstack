using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

[DebuggerDisplay("Type = {GetType().Name,nq}, TargetResource = {TargetResource}")]
internal sealed class LocalStackReferenceAnnotation(string targetResource) : IResourceAnnotation
{
    public string TargetResource { get; } = targetResource;
}
