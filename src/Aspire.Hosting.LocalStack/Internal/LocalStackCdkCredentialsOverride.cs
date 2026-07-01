using Amazon;
using Amazon.Runtime;
using LocalStack.Client.Contracts;

namespace Aspire.Hosting.LocalStack.Internal;

/// <summary>
/// Installs a process-wide AWS SDK credential generator that returns LocalStack session credentials.
/// </summary>
/// <remarks>
/// The CDK asset uploader builds its own STS and S3 clients and resolves credentials eagerly at
/// construction via the default credential chain, which throws when no real credentials are present
/// and resolves real SSO/profile credentials when they are. Replacing the chain here makes that
/// resolution return LocalStack credentials instead. This is process-global by AWS SDK design; with
/// LocalStack enabled, AppHost-side AWS credential resolution is LocalStack's for the AppHost lifetime.
/// </remarks>
internal static class LocalStackCdkCredentialsOverride
{
    internal static void Apply(ILocalStackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var session = options.Session;

        AWSConfigs.AWSCredentialsGenerators =
        [
            () => new SessionAWSCredentials(session.AwsAccessKeyId, session.AwsAccessKey, session.AwsSessionToken),
        ];
    }
}
