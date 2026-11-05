using System.Diagnostics;
using System.Text.Json.Nodes;

namespace LocalStack.Tools;

internal static class RuntimeRunner
{
    internal static async Task<int> RunAsync(string root, string runtime, string outputPath, CancellationToken cancellationToken)
    {
        var secret = GetActivationToken(runtime);
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
        var revision = await ToolApp.GitAsync(root, "rev-parse", "HEAD").ConfigureAwait(false);
        var clean = string.IsNullOrEmpty(await ToolApp.GitAsync(root, "status", "--porcelain", "--untracked-files=all").ConfigureAwait(false));
        var record = new JsonObject
        {
            ["schema_version"] = 1,
            ["runtime_id"] = runtime,
            ["revision"] = revision,
            ["source_clean"] = clean,
            ["configuration"] = "Release/net10.0",
            ["status"] = "incomplete",
        };
        await WriteRecordAsync().ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Runtime {runtime} started PID={Environment.ProcessId}").ConfigureAwait(false);
        var info = CreateCommand(root, runtime, reports, temp, credentials, secret, manifest["scenarios"]!.AsArray().Count);
        var completed = false;
        try
        {
            var result = await ProcessRunner.RunAsync(info, secret, Path.Combine(reports, "test.log"), echo: true, cancellationToken).ConfigureAwait(false);
            await ReportRedaction.RedactDirectoryAsync(reports, secret).ConfigureAwait(false);
            await ReportRedaction.RedactSummaryAsync(secret).ConfigureAwait(false);

            ProcessRunner.CopyDirectory(reports, output);
            completed = !Directory.EnumerateDirectories(temp, "localstack-tests-*").Any();
            if (!completed)
            {
                throw new IOException($"Session cleanup did not finish; owned scratch retained: {scratch.FullName}");
            }

            if (result.ExitCode == 0)
            {
                await RuntimeEvidence.ValidateResultsAsync(output, runtime, manifest, images).ConfigureAwait(false);
            }

            record["source_clean"] = clean && string.IsNullOrEmpty(await ToolApp.GitAsync(root, "status", "--porcelain", "--untracked-files=all").ConfigureAwait(false))
                && revision == await ToolApp.GitAsync(root, "rev-parse", "HEAD").ConfigureAwait(false);
            record["status"] = result.ExitCode == 0 ? "passed" : "failed";
            return result.ExitCode;
        }
        finally
        {
            if (completed)
            {
                scratch.Delete(recursive: true);
            }
            else
            {
                await Console.Error.WriteLineAsync($"Incomplete check scratch retained: {scratch.FullName}").ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                record["status"] = "cancelled";
            }

            await WriteRecordAsync().ConfigureAwait(false);
        }

        Task WriteRecordAsync() => File.WriteAllTextAsync(Path.Combine(output, "profile-run.json"), record.ToJsonString(), CancellationToken.None);
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
