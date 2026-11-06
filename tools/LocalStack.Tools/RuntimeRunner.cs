using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace LocalStack.Tools;

internal static class RuntimeRunner
{
    internal static Task<int> RunAsync(string root, string runtime, string outputPath, CancellationToken cancellationToken)
    {
        var secret = GetActivationToken(runtime);
        return RunAsync(root, runtime, outputPath, secret,
            Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY"),
            (info, token) => ProcessRunner.RunAsync(info, secret,
                Path.Combine(info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!, "test.log"), echo: true, token),
            args => ToolApp.GitAsync(root, args), cancellationToken);
    }

    internal static async Task<int> RunAsync(string root, string runtime, string outputPath,
        string? secret, string? summaryDestination,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string Output)>> execute,
        Func<string[], Task<string>> git, CancellationToken cancellationToken)
    {
        var output = ToolApp.NewOutput(outputPath);
        var manifest = await RuntimeEvidence.ReadAsync(Path.Combine(root, ".github/scripts/common_runtime_tests.json")).ConfigureAwait(false);
        var images = await RuntimeEvidence.ReadAsync(Path.Combine(root, "tests/Aspire.Hosting.LocalStack.Integration.Tests/TestInfrastructure/runtime-images.json")).ConfigureAwait(false);
        var scratch = Directory.CreateTempSubdirectory("localstack-runtime-");
        var reports = Path.Combine(scratch.FullName, "results");
        var temp = Path.Combine(scratch.FullName, "temp");
        Directory.CreateDirectory(reports);
        Directory.CreateDirectory(temp);
        var credentials = Path.Combine(scratch.FullName, "empty-aws");
        await File.WriteAllTextAsync(credentials, string.Empty, cancellationToken).ConfigureAwait(false);
        var revision = await git(["rev-parse", "HEAD"]).ConfigureAwait(false);
        var clean = string.IsNullOrEmpty(await git(["status", "--porcelain", "--untracked-files=all"]).ConfigureAwait(false));
        var executionId = Guid.NewGuid().ToString();
        var summary = Path.Combine(scratch.FullName, "summary.md");
        var record = new JsonObject
        {
            ["schema_version"] = 2,
            ["runtime_id"] = runtime,
            ["revision"] = revision,
            ["source_clean"] = clean,
            ["configuration"] = "Release/net10.0",
            ["status"] = "incomplete",
            ["execution_id"] = executionId,
            ["artifact_sha256"] = new JsonObject(),
        };
        await WriteRecordAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Runtime {runtime} started PID={Environment.ProcessId}").ConfigureAwait(false);
        var info = CreateCommand(root, runtime, reports, temp, credentials, secret, manifest["scenarios"]!.AsArray().Count);
        info.Environment["GITHUB_STEP_SUMMARY"] = summary;
        info.Environment["LOCALSTACK_TEST_EXECUTION_ID"] = executionId;
        var completed = false;
        var failed = false;
        string? summaryFailure;
        var exitCode = -1;
        try
        {
            exitCode = (await execute(info, cancellationToken).ConfigureAwait(false)).ExitCode;
            await ProcessReportsAsync(reports, output, temp, secret, runtime, manifest, images, record, git, clean, revision, exitCode).ConfigureAwait(false);
            completed = true;
        }
        catch (Exception)
        {
            failed = true;
            record["status"] = "failed";
            throw;
        }
        finally
        {
            summaryFailure = await ReportRedaction.RedactSummaryAsync(summary, summaryDestination, secret).ConfigureAwait(false);
            completed &= summaryFailure is null;
            if (summaryFailure is not null)
            {
                record["status"] = "failed";
                await Console.Error.WriteLineAsync(summaryFailure).ConfigureAwait(false);
            }

            await FinalizeAsync(output, scratch, record, completed, failed, cancellationToken).ConfigureAwait(false);
        }

        if (summaryFailure is not null)
        {
            throw new InvalidDataException(summaryFailure);
        }

        return exitCode;

        Task WriteRecordAsync() => File.WriteAllTextAsync(Path.Combine(output, "profile-run.json"), record.ToJsonString(), CancellationToken.None);
    }

    private static async Task ProcessReportsAsync(string reports, string output, string temp, string? secret,
        string runtime, JsonNode manifest, JsonNode images, JsonObject record, Func<string[], Task<string>> git,
        bool clean, string revision, int exitCode)
    {
        await ReportRedaction.RedactDirectoryAsync(reports, secret).ConfigureAwait(false);
        var hashes = record["artifact_sha256"]!.AsObject();
        foreach (var path in Directory.EnumerateFiles(reports, "*-image-*.json", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(reports, "runtime.trx", SearchOption.TopDirectoryOnly)))
        {
            hashes[Path.GetRelativePath(reports, path).Replace('\\', '/')] = Convert.ToHexString(
                SHA256.HashData(await File.ReadAllBytesAsync(path, CancellationToken.None).ConfigureAwait(false)));
        }

        ProcessRunner.CopyDirectory(reports, output);
        if (Directory.EnumerateDirectories(temp, "localstack-tests-*").Any())
        {
            throw new IOException($"Session cleanup did not finish; owned scratch retained: {Path.GetDirectoryName(temp)}");
        }

        if (exitCode == 0)
        {
            await RuntimeEvidence.ValidateResultsAsync(reports, runtime, manifest, images, record).ConfigureAwait(false);
        }

        record["source_clean"] = clean && string.IsNullOrEmpty(await git(["status", "--porcelain", "--untracked-files=all"]).ConfigureAwait(false))
            && revision == await git(["rev-parse", "HEAD"]).ConfigureAwait(false);
        record["status"] = exitCode == 0 ? "passed" : "failed";
    }

    private static async Task FinalizeAsync(string output, DirectoryInfo scratch, JsonObject record,
        bool completed, bool failed, CancellationToken cancellationToken)
    {
        Exception? cleanupFailure = null;
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                record["status"] = "cancelled";
            }

            if (completed)
            {
                try
                {
                    scratch.Delete(recursive: true);
                }
                catch (IOException)
                {
                    record["status"] = "failed";
                    cleanupFailure = new IOException("Runtime scratch cleanup failed.");
                }
                catch (UnauthorizedAccessException)
                {
                    record["status"] = "failed";
                    cleanupFailure = new IOException("Runtime scratch cleanup failed.");
                }
            }

            await File.WriteAllTextAsync(Path.Combine(output, "profile-run.json"), record.ToJsonString(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) when (failed)
        {
            await Console.Error.WriteLineAsync("Runtime execution record finalization failed.").ConfigureAwait(false);
        }
        finally
        {
            if (!completed || cleanupFailure is not null)
            {
                await Console.Error.WriteLineAsync($"Incomplete check scratch retained: {scratch.FullName}").ConfigureAwait(false);
            }
        }

        if (cleanupFailure is not null)
        {
            throw cleanupFailure;
        }
    }

    private static string? GetActivationToken(string runtime)
    {
        if (runtime is not ("current" or "legacy"))
        {
            throw new ArgumentException("Runtime must be current or legacy.", nameof(runtime));
        }

        var secret = runtime == "current" ? Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN") : null;
        if (runtime == "current" && string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Current runtime credentials are missing; no tests ran.");
        }

        // Legacy/unit jobs retain their native automatic upload. An authenticated job must
        // publish only after masking; an already configured immediate uploader cannot do that.
        if (secret is not null && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ACTIONS_RUNTIME_TOKEN")))
        {
            throw new InvalidOperationException("Immediate HTML upload is configured before credential masking. No authenticated test or upload was started.");
        }

        return secret;
    }

    private static ProcessStartInfo CreateCommand(string root, string runtime, string reports, string temp,
        string credentials, string? secret, int minimumTests)
    {
        var project = Path.Combine(root, "tests/Aspire.Hosting.LocalStack.Integration.Tests/Aspire.Hosting.LocalStack.Integration.Tests.csproj");
        var info = ProcessRunner.Create("dotnet", root,
        [
            "test", "--project", project, "--configuration", "Release", "--framework", "net10.0",
            "--report-trx", "--report-trx-filename", "runtime.trx", "--results-directory", reports,
            "--", "--maximum-parallel-tests", "1", "--minimum-expected-tests",
            minimumTests.ToString(System.Globalization.CultureInfo.InvariantCulture), "--timeout", "10m",
        ]);
        info.Environment["LOCALSTACK_TEST_RUNTIME"] = runtime;
        info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"] = reports;
        info.Environment["AWS_ACCESS_KEY_ID"] = "test";
        info.Environment["AWS_SECRET_ACCESS_KEY"] = "test";
        info.Environment["AWS_REGION"] = "us-east-1";
        info.Environment["AWS_DEFAULT_REGION"] = "us-east-1";
        info.Environment["AWS_EC2_METADATA_DISABLED"] = "true";
        info.Environment["AWS_SHARED_CREDENTIALS_FILE"] = credentials;
        info.Environment["AWS_CONFIG_FILE"] = credentials;
        info.Environment["TMPDIR"] = temp;
        info.Environment["TEMP"] = temp;
        info.Environment["TMP"] = temp;
        if (secret is not null)
        {
            info.Environment["LOCALSTACK_AUTH_TOKEN"] = secret;
        }

        return info;
    }
}
