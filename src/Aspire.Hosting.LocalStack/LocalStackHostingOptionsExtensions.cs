#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.LocalStack;

namespace Aspire.Hosting;

/// <summary>
/// Fluent extension methods for <see cref="LocalStackHostingOptions"/>.
/// </summary>
public static class LocalStackHostingOptionsExtensions
{
    /// <summary>
    /// Sets whether LocalStack is enabled.
    /// </summary>
    /// <param name="options">The hosting options instance.</param>
    /// <param name="enabled"><see langword="true"/> to enable LocalStack; <see langword="false"/> to disable.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public static LocalStackHostingOptions WithEnabled(this LocalStackHostingOptions options, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Enabled = enabled;
        return options;
    }

    /// <summary>
    /// Sets the AWS region for LocalStack sessions.
    /// </summary>
    /// <param name="options">The hosting options instance.</param>
    /// <param name="region">The AWS region name (e.g., <c>"us-east-1"</c>, <c>"eu-central-1"</c>).</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="region"/> is empty or whitespace.</exception>
    public static LocalStackHostingOptions WithRegion(this LocalStackHostingOptions options, string region)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);

        options.Region = region;
        return options;
    }

    /// <summary>
    /// Sets the AWS credentials for LocalStack sessions.
    /// </summary>
    /// <param name="options">The hosting options instance.</param>
    /// <param name="accessKeyId">The AWS access key ID.</param>
    /// <param name="secretAccessKey">The AWS secret access key.</param>
    /// <param name="sessionToken">The AWS session token.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessKeyId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="secretAccessKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sessionToken"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any credential argument is empty or whitespace.</exception>
    public static LocalStackHostingOptions WithCredentials(
        this LocalStackHostingOptions options,
        string accessKeyId,
        string secretAccessKey,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(accessKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessKeyId);
        ArgumentNullException.ThrowIfNull(secretAccessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretAccessKey);
        ArgumentNullException.ThrowIfNull(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        options.AccessKeyId = accessKeyId;
        options.SecretAccessKey = secretAccessKey;
        options.SessionToken = sessionToken;
        return options;
    }
}
#pragma warning restore IDE0130
