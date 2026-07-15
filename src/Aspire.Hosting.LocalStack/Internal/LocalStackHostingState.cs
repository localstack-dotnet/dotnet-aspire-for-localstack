namespace Aspire.Hosting.LocalStack.Internal;

/// <summary>
/// Immutable snapshot of resolved LocalStack hosting configuration. Produced by
/// <see cref="LocalStackHostingOptionsResolver"/> after section binding, callback overrides,
/// AWS SDK region overrides, and validation have all run.
/// </summary>
internal sealed record LocalStackHostingState
{
    /// <summary>
    /// Whether LocalStack hosting is enabled. When <see langword="false"/>, the resolver
    /// returns no state (<see langword="null"/>), so a state value with <c>Enabled = false</c>
    /// is never produced.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>AWS region system name (e.g. <c>"us-east-1"</c>) used by LocalStack sessions.</summary>
    public required string Region { get; init; }

    /// <summary>AWS access key ID forwarded to LocalStack sessions.</summary>
    public required string AccessKeyId { get; init; }

    /// <summary>AWS secret access key forwarded to LocalStack sessions.</summary>
    public required string SecretAccessKey { get; init; }

    /// <summary>AWS session token forwarded to LocalStack sessions.</summary>
    public required string SessionToken { get; init; }

    /// <summary>
    /// Legacy compatibility flag sourced from <c>LocalStack:Config:UseSsl</c> in the
    /// <c>LocalStack.Client</c> section. Not surfaced on <see cref="LocalStackHostingOptions"/>;
    /// preserved here so downstream LocalStack.Client wiring can still observe it.
    /// </summary>
    public required bool UseSsl { get; init; }

    /// <summary>
    /// Legacy compatibility flag sourced from <c>LocalStack:Config:UseLegacyPorts</c> in the
    /// <c>LocalStack.Client</c> section. Not surfaced on <see cref="LocalStackHostingOptions"/>;
    /// preserved here so downstream LocalStack.Client wiring can still observe it.
    /// </summary>
    public required bool UseLegacyPorts { get; init; }
}
