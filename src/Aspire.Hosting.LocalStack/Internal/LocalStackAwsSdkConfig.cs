using Amazon;
using Aspire.Hosting.AWS;

namespace Aspire.Hosting.LocalStack.Internal;

internal sealed class LocalStackAwsSdkConfig(RegionEndpoint region, bool sdkValidationEnabled) : IAWSSDKConfig
{
    /// <summary>Profile is intentionally null: LocalStack credentials are supplied by the global credential generator.</summary>
    public string? Profile { get; set; }

    public RegionEndpoint? Region { get; set; } = region;

    public bool SDKValidationEnabled { get; set; } = sdkValidationEnabled;
}
