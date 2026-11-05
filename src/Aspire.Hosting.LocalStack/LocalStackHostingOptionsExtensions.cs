#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack;
using Aspire.Hosting.LocalStack.Internal;

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

/// <summary>
/// Fluent configuration for LocalStack container runtime selection.
/// </summary>
public static class LocalStackContainerOptionsExtensions
{
    /// <summary>
    /// Selects an explicit container activation token above environment and host configuration.
    /// </summary>
    /// <param name="options">The container options.</param>
    /// <param name="authToken">The activation token. Blank input suppresses lower-priority sources.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static LocalStackContainerOptions WithAuthToken(this LocalStackContainerOptions options, string authToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AuthTokenSource = LocalStackAuthTokenSource.FromLiteral(authToken);
        return options;
    }

    /// <summary>
    /// Selects a deferred container activation parameter above environment and host configuration.
    /// </summary>
    /// <param name="options">The container options.</param>
    /// <param name="authToken">The secret activation parameter. Its value is not resolved here.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the parameter is not marked secret.</exception>
    public static LocalStackContainerOptions WithAuthToken(
        this LocalStackContainerOptions options,
        IResourceBuilder<ParameterResource> authToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AuthTokenSource = LocalStackAuthTokenSource.FromParameter(authToken);
        return options;
    }

    /// <summary>
    /// Selects the legacy runtime preset without changing explicit container image overrides.
    /// </summary>
    /// <param name="options">The container options.</param>
    /// <param name="enabled">Whether to select the legacy preset instead of the current preset.</param>
    /// <returns>The same <paramref name="options"/> instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public static LocalStackContainerOptions WithLegacy(this LocalStackContainerOptions options, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RuntimePreset = enabled ? LocalStackRuntimePreset.Legacy : LocalStackRuntimePreset.Current;
        return options;
    }
}
#pragma warning restore IDE0130
