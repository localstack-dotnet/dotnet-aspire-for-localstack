using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Marks resources that have a LocalStack native endpoint conflict warning callback registered.
/// </summary>
/// <param name="callback">The registered warning callback.</param>
internal sealed class LocalStackEndpointConflictWarningAnnotation(EnvironmentCallbackAnnotation callback) : IResourceAnnotation
{
    /// <summary>
    /// Gets the environment callback that inspects the workload environment and logs the warning.
    /// </summary>
    public EnvironmentCallbackAnnotation Callback { get; } = callback;
}
