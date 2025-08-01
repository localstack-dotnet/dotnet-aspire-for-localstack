namespace Aspire.Hosting.LocalStack.Container;

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
