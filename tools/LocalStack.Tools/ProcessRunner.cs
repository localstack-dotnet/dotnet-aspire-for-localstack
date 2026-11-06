using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace LocalStack.Tools;

internal static class ProcessRunner
{
    internal static ProcessStartInfo Create(string executable, string workingDirectory, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // Child SDKs must not reuse a server holding a different process environment.
        foreach (var key in info.Environment.Keys.Where(key => key.StartsWith("AWS_", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("LOCALSTACK_", StringComparison.OrdinalIgnoreCase)
                     || key.StartsWith("ASPIRE__HOSTING__LOCALSTACK__", StringComparison.OrdinalIgnoreCase)
                     || key is "DOTNET_STARTUP_HOOKS" or "DOTNET_ADDITIONAL_DEPS" or "DOTNET_SHARED_STORE" or "LD_PRELOAD").ToArray())
        {
            info.Environment.Remove(key);
        }

        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        info.Environment["UseSharedCompilation"] = "false";
        return info;
    }

    internal static async Task<(int ExitCode, string Output)> RunAsync(
        ProcessStartInfo info, string? secret, string? logPath, bool echo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = info };
        ExceptionDispatchInfo? failure = null;
        (int ExitCode, string Output) result = default;
        try
        {
            await using var writer = logPath is null ? TextWriter.Null : new StreamWriter(logPath);
            try
            {
                result = await RunAsync(() => process.Start(), process.WaitForExitAsync,
                    () => process.Kill(entireProcessTree: true), () => process.HasExited, () => process.ExitCode,
                    () => process.StandardOutput.ReadLineAsync(CancellationToken.None).AsTask(),
                    () => process.StandardError.ReadLineAsync(CancellationToken.None).AsTask(),
                    writer, secret, echo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        }
        catch (IOException exception) when (failure is not null)
        {
            await Console.Error.WriteLineAsync($"Secondary {exception.GetType().Name}: {ReportRedaction.Mask(exception.Message, secret)}").ConfigureAwait(false);
        }

        failure?.Throw();
        return result;
    }

    internal static async Task<(int ExitCode, string Output)> RunAsync(
        Action start, Func<CancellationToken, Task> wait, Action kill, Func<bool> hasExited, Func<int> exitCode,
        Func<Task<string?>> readOutput, Func<Task<string?>> readError, TextWriter writer,
        string? secret, bool echo, CancellationToken cancellationToken)
    {
        var log = TextWriter.Synchronized(writer);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var output = Task.CompletedTask;
        var error = Task.CompletedTask;
        var exited = Task.CompletedTask;
        (int ExitCode, string Output) result;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            start();
            output = PumpAsync(readOutput, stdout, log, secret, echo);
            error = PumpAsync(readError, stderr, log, secret, echo);
            exited = wait(cancellationToken);
            var pending = new List<Task> { output, error, exited };
            while (pending.Count > 0)
            {
                var finished = await Task.WhenAny(pending).ConfigureAwait(false);
                pending.Remove(finished);
                await finished.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            result = (exitCode(), stdout.ToString() + stderr);
        }
        catch (Exception)
        {
            await CleanupAsync(wait, kill, hasExited, secret).ConfigureAwait(false);
            await Task.WhenAll(output, error, exited).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            throw;
        }

        return result;
    }

    private static async Task CleanupAsync(Func<CancellationToken, Task> wait, Action kill, Func<bool> hasExited, string? secret)
    {
        try
        {
            if (!hasExited())
            {
                kill();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            await Console.Error.WriteLineAsync($"Secondary {exception.GetType().Name}: {ReportRedaction.Mask(exception.Message, secret)}").ConfigureAwait(false);
        }

        await wait(CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private static async Task PumpAsync(Func<Task<string?>> read, StringBuilder captured, TextWriter log, string? secret, bool echo)
    {
        while (await read().ConfigureAwait(false) is { } line)
        {
            var safe = ReportRedaction.Mask(line, secret);
            captured.AppendLine(safe);
            await log.WriteLineAsync(safe).ConfigureAwait(false);
            if (echo)
            {
                await Console.Out.WriteLineAsync(safe).ConfigureAwait(false);
            }
        }
    }

    internal static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(path, target);
        }
    }
}
