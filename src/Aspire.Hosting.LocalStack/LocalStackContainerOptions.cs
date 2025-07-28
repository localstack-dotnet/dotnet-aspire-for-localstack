#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack;

/// <summary>
/// Configuration options for LocalStack container behavior in Aspire.
/// </summary>
public sealed class LocalStackContainerOptions
{
    /// <summary>
    /// Gets or sets the container lifetime behavior.
    /// </summary>
    /// <remarks>
    /// - <see cref="ContainerLifetime.Persistent"/>: Container survives application restarts (default for databases)
    /// - <see cref="ContainerLifetime.Session"/>: Container is cleaned up when application stops (recommended for LocalStack)
    /// - <see cref="ContainerLifetime.Transient"/>: Container is recreated on each run
    /// </remarks>
    public ContainerLifetime Lifetime { get; set; } = ContainerLifetime.Persistent;

    /// <summary>
    /// Gets or sets the DEBUG environment variable value for LocalStack.
    /// </summary>
    /// <remarks>
    /// 0 = Default log level (default)
    /// 1 = Increased log level with more verbose logs (useful for troubleshooting)
    /// </remarks>
    public int DebugLevel { get; set; }

    /// <summary>
    /// Gets or sets the LS_LOG environment variable value for LocalStack.
    /// </summary>
    /// <remarks>
    /// Controls the LocalStack log level. Currently overrides the DEBUG configuration.
    /// - trace: Detailed request/response logging
    /// - trace-internal: Internal calls logging
    /// - debug: Debug level logging
    /// - info: Info level logging (default)
    /// - warn: Warning level logging
    /// - error: Error level logging
    /// - warning: Warning level logging (alias for warning)
    /// </remarks>
    public LocalStackLogLevel LogLevel { get; set; } = LocalStackLogLevel.Error;

    /// <summary>
    /// Gets or sets additional environment variables to pass to the LocalStack container.
    /// </summary>
    public IDictionary<string, string> AdditionalEnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Represents the available log levels for LocalStack.
/// </summary>
public enum LocalStackLogLevel
{
    /// <summary>
    /// Detailed request/response logging.
    /// </summary>
    Trace,

    /// <summary>
    /// Internal calls logging.
    /// </summary>
    TraceInternal,

    /// <summary>
    /// Debug level logging.
    /// </summary>
    Debug,

    /// <summary>
    /// Info level logging (default).
    /// </summary>
    Info,

    /// <summary>
    /// Warning level logging.
    /// </summary>
    Warn,

    /// <summary>
    /// Error level logging.
    /// </summary>
    Error,

    /// <summary>
    /// Warning level logging (alias for Warn).
    /// </summary>
    Warning,
}

/// <summary>
/// Extension methods for LocalStackLogLevel enum.
/// </summary>
internal static class LocalStackLogLevelExtensions
{
    /// <summary>
    /// Converts the LocalStackLogLevel enum to its string representation for the LS_LOG environment variable.
    /// </summary>
    /// <param name="logLevel">The log level to convert.</param>
    /// <returns>The string representation of the log level.</returns>
    public static string ToEnvironmentValue(this LocalStackLogLevel logLevel)
    {
        return logLevel switch
        {
            LocalStackLogLevel.Trace => "trace",
            LocalStackLogLevel.TraceInternal => "trace-internal",
            LocalStackLogLevel.Debug => "debug",
            LocalStackLogLevel.Info => "info",
            LocalStackLogLevel.Warn => "warn",
            LocalStackLogLevel.Error => "error",
            LocalStackLogLevel.Warning => "warning",
            _ => "info",
        };
    }
}
