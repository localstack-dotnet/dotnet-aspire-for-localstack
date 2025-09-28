namespace Aspire.Hosting.LocalStack.Internal;

internal static class Constants
{
    // Internal port is always 4566.
    internal const int DefaultContainerPort = 4566;
    internal const string CloudFormationReferenceAnnotation = "Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation";
    internal const string SQSEventSourceResource = "Aspire.Hosting.AWS.Lambda.SQSEventSourceResource";
}
