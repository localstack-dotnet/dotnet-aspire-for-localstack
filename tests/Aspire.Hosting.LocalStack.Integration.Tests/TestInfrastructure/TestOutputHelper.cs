namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Helper for safely writing test output, handling cases where TestContext.Current may be null.
/// </summary>
internal static class TestOutputHelper
{
    /// <summary>
    /// Safely writes a line to the test output. Does nothing if TestContext.Current is null.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public static async Task WriteLineAsync(string message)
    {
        if (TestContext.Current is { } context)
        {
            await context.OutputWriter.WriteLineAsync(message);
        }
    }

    /// <summary>
    /// Safely writes a line to the test output using an interpolated string.
    /// Does nothing if TestContext.Current is null.
    /// </summary>
    /// <param name="handler">The interpolated string handler.</param>
    public static async Task WriteLineAsync(FormattableString handler)
    {
        if (TestContext.Current is { } context)
        {
            await context.OutputWriter.WriteLineAsync(handler.ToString(CultureInfo.InvariantCulture));
        }
    }
}

