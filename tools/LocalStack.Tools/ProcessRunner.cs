using System.Diagnostics;
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
        await using var file = logPath is null ? null : new StreamWriter(logPath);
        var log = file is null ? TextWriter.Null : TextWriter.Synchronized(file);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.Start();
        var output = PumpAsync(process.StandardOutput, stdout);
        var error = PumpAsync(process.StandardError, stderr);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(output, error).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return (process.ExitCode, stdout.ToString() + stderr);

        async Task PumpAsync(StreamReader reader, StringBuilder captured)
        {
            while (await reader.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
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
