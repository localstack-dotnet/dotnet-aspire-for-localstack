using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Marker annotation that subscribes the DynamoDB Local fail-fast guard to a LocalStack resource.
/// Added once per LocalStack resource; the guard scans the final app model on BeforeStartEvent
/// and throws if AddAWSDynamoDBLocal is present alongside UseLocalStack().
/// </summary>
internal sealed class DynamoDbLocalGuardAnnotation : IResourceAnnotation;
