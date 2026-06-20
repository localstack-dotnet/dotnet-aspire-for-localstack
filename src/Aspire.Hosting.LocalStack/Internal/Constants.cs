namespace Aspire.Hosting.LocalStack.Internal;

internal static class Constants
{
    /// <summary>
    /// Internal port is always 4566.
    /// </summary>
    internal const int DefaultContainerPort = 4566;
    internal const string CloudFormationReferenceAnnotation = "Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation";
    internal const string SQSEventSourceResource = "Aspire.Hosting.AWS.Lambda.SQSEventSourceResource";

    internal const string LocalStackHealthClientName = "localstack_health_client";
    internal const string LocalStackHealthCheckName = "localstack_health";
}
