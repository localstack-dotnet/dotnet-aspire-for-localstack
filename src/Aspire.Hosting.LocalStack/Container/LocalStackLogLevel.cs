namespace Aspire.Hosting.LocalStack.Container;

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
