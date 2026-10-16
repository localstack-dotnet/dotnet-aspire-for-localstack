using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Marks resources that have a LocalStack native endpoint conflict warning callback registered.
/// </summary>
internal sealed class LocalStackEndpointConflictWarningAnnotation : IResourceAnnotation;
