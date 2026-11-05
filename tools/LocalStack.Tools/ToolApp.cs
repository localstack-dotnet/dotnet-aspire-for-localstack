using System.Text.Json;
using System.Xml;

namespace LocalStack.Tools;

/// <summary>
/// Project-specific runtime evidence and packed-consumer checks around standard .NET commands.
/// </summary>
public static class ToolApp
{
    /// <summary>
    /// Runs the selected repository check, preserving diagnostics and nonzero failures.
    /// </summary>
    /// <param name="args">The command and its paths.</param>
    /// <returns>Zero only when the requested check completes successfully.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.CancelAfter(TimeSpan.Zero); };
        Console.CancelKeyPress += handler;
        try
        {
            var root = FindRoot();
            var exitCode = args switch
            {
                ["run-runtime", var runtime, var output] => await RuntimeRunner.RunAsync(root, runtime, output, cancellation.Token).ConfigureAwait(false),
                ["validate-runtime", var current, var legacy] => await ValidatePairAsync(root, current, legacy).ConfigureAwait(false),
                ["verify-consumer", var packages, var output] => await ConsumerVerification.RunAsync(root, packages, output, cancellation.Token).ConfigureAwait(false),
                _ => throw new ArgumentException("Usage: run-runtime <current|legacy> <output> | validate-runtime <current-dir> <legacy-dir> | verify-consumer <package-dir> <output>", nameof(args)),
            };
            cancellation.Token.ThrowIfCancellationRequested();
            return exitCode;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Check cancelled; retained scratch directories are not acceptance evidence.").ConfigureAwait(false);
            return 130;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException
            or JsonException or XmlException or System.ComponentModel.Win32Exception)
        {
            var secret = args is ["run-runtime", "current", ..] ? Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN") : null;
            await Console.Error.WriteLineAsync(ReportRedaction.Mask(error.Message, secret)).ConfigureAwait(false);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    internal static string FindRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(directory.FullName, "LocalStack.slnx")))
        {
            directory = directory.Parent ?? throw new InvalidOperationException("Run this tool inside the LocalStack repository.");
        }

        return directory.FullName;
    }

    internal static async Task<string> GitAsync(string root, params string[] arguments)
    {
        var result = await ProcessRunner.RunAsync(ProcessRunner.Create("git", root, arguments), null, null, echo: false, CancellationToken.None).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.Output.Trim() : throw new InvalidOperationException(result.Output);
    }

    internal static string NewOutput(string path)
    {
        var output = Path.GetFullPath(path);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new IOException("The output directory is not empty; existing evidence was not changed.");
        }

        Directory.CreateDirectory(output);
        return output;
    }

    private static async Task<int> ValidatePairAsync(string root, string current, string legacy)
    {
        var manifest = await RuntimeEvidence.ReadAsync(Path.Combine(root, ".github/scripts/common_runtime_tests.json")).ConfigureAwait(false);
        var images = await RuntimeEvidence.ReadAsync(Path.Combine(root, "tests/Aspire.Hosting.LocalStack.Integration.Tests/TestInfrastructure/runtime-images.json")).ConfigureAwait(false);
        var revision = await GitAsync(root, "rev-parse", "HEAD").ConfigureAwait(false);
        var count = await RuntimeEvidence.ValidatePairAsync(current, legacy, revision, manifest, images).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Accepted both clean runtime runs: {count} matching tests per runtime.").ConfigureAwait(false);
        return 0;
    }
}
