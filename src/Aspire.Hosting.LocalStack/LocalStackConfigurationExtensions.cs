using LocalStack.Client.Contracts;
using LocalStack.Client.Options;

namespace Aspire.Hosting.LocalStack;

/// <summary>
/// Extension methods for configuring LocalStack options using the fluent API.
/// Based on LocalStack.Client configuration patterns.
/// </summary>
public static class LocalStackConfigurationExtensions
{
    /// <summary>
    /// Sets whether to use LocalStack instead of real AWS services.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="useLocalStack">Whether to use LocalStack.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithUseLocalStack(this ILocalStackOptions options, bool useLocalStack)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new LocalStackOptions(useLocalStack, options.Session, options.Config);
    }

    /// <summary>
    /// Sets the edge port for LocalStack.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="edgePort">The edge port to use.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithEdgePort(this ILocalStackOptions options, int edgePort)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configOptions = new ConfigOptions(
            options.Config.LocalStackHost,
            options.Config.UseSsl,
            options.Config.UseLegacyPorts,
            edgePort);

        return new LocalStackOptions(options.UseLocalStack, options.Session, configOptions);
    }

    /// <summary>
    /// Sets the LocalStack host.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="localStackHost">The LocalStack host to use.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithLocalStackHost(this ILocalStackOptions options, string localStackHost)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(localStackHost);

        var configOptions = new ConfigOptions(
            localStackHost,
            options.Config.UseSsl,
            options.Config.UseLegacyPorts,
            options.Config.EdgePort);

        return new LocalStackOptions(options.UseLocalStack, options.Session, configOptions);
    }

    /// <summary>
    /// Sets whether to use SSL for LocalStack.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="useSsl">Whether to use SSL.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithUseSsl(this ILocalStackOptions options, bool useSsl)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configOptions = new ConfigOptions(
            options.Config.LocalStackHost,
            useSsl,
            options.Config.UseLegacyPorts,
            options.Config.EdgePort);

        return new LocalStackOptions(options.UseLocalStack, options.Session, configOptions);
    }

    /// <summary>
    /// Sets the session options for LocalStack.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="sessionOptions">The session options to use.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithSessionOptions(this ILocalStackOptions options, SessionOptions sessionOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sessionOptions);

        return new LocalStackOptions(options.UseLocalStack, sessionOptions, options.Config);
    }

    /// <summary>
    /// Sets the config options for LocalStack.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="configOptions">The config options to use.</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithConfigOptions(this ILocalStackOptions options, ConfigOptions configOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configOptions);

        return new LocalStackOptions(options.UseLocalStack, options.Session, configOptions);
    }

    /// <summary>
    /// Sets the AWS region for LocalStack sessions.
    /// </summary>
    /// <param name="options">The LocalStack options.</param>
    /// <param name="regionName">The AWS region name (e.g., "us-east-1", "eu-central-1").</param>
    /// <returns>New LocalStack options with updated settings.</returns>
    public static ILocalStackOptions WithRegion(this ILocalStackOptions options, string regionName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionName);

        var sessionOptions = new SessionOptions(
            options.Session.AwsAccessKeyId,
            options.Session.AwsAccessKey,
            options.Session.AwsSessionToken,
            regionName);

        return new LocalStackOptions(options.UseLocalStack, sessionOptions, options.Config);
    }
}
