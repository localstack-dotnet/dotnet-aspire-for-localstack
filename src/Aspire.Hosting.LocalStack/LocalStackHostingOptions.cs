namespace Aspire.Hosting.LocalStack;

/// <summary>
/// Package-owned options for configuring a LocalStack resource.
/// Designed to be used with ConfigurationBinder, C# callbacks, and fluent extension methods.
/// Default values match LocalStack.Client's built-in defaults for backward compatibility.
/// </summary>
public sealed class LocalStackHostingOptions
{
    /// <summary>
    /// Gets or sets whether LocalStack is enabled.
    /// When <see langword="false"/>, LocalStack resource creation
    /// returns <see langword="null"/> without allocating a container.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the AWS region used by LocalStack sessions.
    /// Default: <c>"us-east-1"</c>.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Gets or sets the AWS access key ID used by LocalStack sessions.
    /// Default: <c>"accessKey"</c>.
    /// </summary>
    public string AccessKeyId { get; set; } = "accessKey";

    /// <summary>
    /// Gets or sets the AWS secret access key used by LocalStack sessions.
    /// Default: <c>"secretKey"</c>.
    /// </summary>
    public string SecretAccessKey { get; set; } = "secretKey";

    /// <summary>
    /// Gets or sets the AWS session token used by LocalStack sessions.
    /// Default: <c>"token"</c>.
    /// </summary>
    public string SessionToken { get; set; } = "token";
}
