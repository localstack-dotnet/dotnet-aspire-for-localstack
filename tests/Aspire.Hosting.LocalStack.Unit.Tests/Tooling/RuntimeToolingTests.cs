using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using TUnit.Assertions.Enums;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using global::LocalStack.Tools;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Tooling;

public sealed class RuntimeToolingTests : IDisposable
{
    private const string Secret = "synthetic-report-secret";
    private readonly List<string> _scratch = [];
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("localstack-tools-tests-");

    [Test]
    public async Task Accepts_Complete_Runtime_Evidence()
    {
        await WriteEvidenceAsync();

        var observed = await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", Manifest(), Images());

        await Assert.That(observed.Count).IsEqualTo(1);
        await Assert.That(observed).Contains(("Example.Tests", "Works", null));
    }

    [Test]
    [Arguments("Failed")]
    [Arguments("NotExecuted")]
    public async Task Rejects_Nonpassing_Outcomes(string outcome)
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, "runtime.trx");
        await File.WriteAllTextAsync(path, Report().Replace("outcome=\"Passed\"", $"outcome=\"{outcome}\"", StringComparison.Ordinal));

        await RefreshHashesAsync(_directory.FullName);
        await Assert.That(async () => { await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", Manifest(), Images()); })
            .Throws<InvalidDataException>();
    }

    [Test]
    [Arguments("revision")]
    [Arguments("dirty")]
    [Arguments("incomplete")]
    [Arguments("missing-report")]
    [Arguments("missing-receipt")]
    [Arguments("wrong-image")]
    [Arguments("wrong-scenario")]
    [Arguments("wrong-runtime")]
    [Arguments("unknown-record-field")]
    [Arguments("unknown-receipt-field")]
    [Arguments("extra-receipt")]
    [Arguments("zero")]
    [Arguments("duplicate-result")]
    public async Task Rejects_Incomplete_Or_Unrelated_Evidence(string defect)
    {
        await WriteEvidenceAsync();
        var recordPath = Path.Combine(_directory.FullName, "profile-run.json");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(recordPath))!;
        switch (defect)
        {
            case "revision": record["revision"] = "another-revision"; break;
            case "dirty": record["source_clean"] = false; break;
            case "incomplete": record["status"] = "incomplete"; break;
            case "wrong-runtime": record["runtime_id"] = "current"; break;
            case "unknown-record-field": record["unexpected"] = "sensitive-content"; break;
            case "missing-report": File.Delete(Path.Combine(_directory.FullName, "runtime.trx")); break;
            case "missing-receipt": File.Delete(Path.Combine(_directory.FullName, "legacy-image-one.json")); break;
            case "wrong-image":
                var receiptPath = Path.Combine(_directory.FullName, "legacy-image-one.json");
                await File.WriteAllTextAsync(receiptPath, (await File.ReadAllTextAsync(receiptPath)).Replace("legacy-tag", "wrong-tag", StringComparison.Ordinal));
                break;
            case "wrong-scenario":
                var reportPath = Path.Combine(_directory.FullName, "runtime.trx");
                await File.WriteAllTextAsync(reportPath, Report().Replace("Works", "AnotherTest", StringComparison.Ordinal));
                break;
            case "unknown-receipt-field":
                var extraPath = Path.Combine(_directory.FullName, "legacy-image-one.json");
                var extraReceipt = JsonNode.Parse(await File.ReadAllTextAsync(extraPath))!;
                extraReceipt["unexpected"] = "sensitive-content";
                await File.WriteAllTextAsync(extraPath, extraReceipt.ToJsonString());
                break;
            case "extra-receipt":
                File.Copy(Path.Combine(_directory.FullName, "legacy-image-one.json"), Path.Combine(_directory.FullName, "legacy-image-two.json"));
                break;
            case "zero":
            case "duplicate-result":
                var nativePath = Path.Combine(_directory.FullName, "runtime.trx");
                var native = XDocument.Parse(await File.ReadAllTextAsync(nativePath));
                var results = native.Descendants().Single(element => element.Name.LocalName == "Results");
                if (defect == "zero")
                {
                    results.RemoveNodes();
                }
                else
                {
                    results.Add(new XElement(results.Elements().Single()));
                }

                await File.WriteAllTextAsync(nativePath, native.ToString());
                break;
        }

        await File.WriteAllTextAsync(recordPath, record.ToJsonString());
        await RefreshHashesAsync(_directory.FullName);
        if (defect == "missing-report")
        {
            await AssertEvidenceFailureAsync("Required evidence is missing or linked: runtime.trx");
            return;
        }

        await Assert.That(async () => { await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", Manifest(), Images()); })
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Redacts_Secrets_Without_Removing_Trx_Diagnostics()
    {
        var path = Path.Combine(_directory.FullName, "runtime.trx");
        await File.WriteAllTextAsync(path, Report());

        await ReportRedaction.RedactDirectoryAsync(_directory.FullName, Secret);

        var text = await File.ReadAllTextAsync(path);
        var document = XDocument.Parse(text);
        await Assert.That(text).DoesNotContain(Secret);
        await Assert.That(text).Contains("license activation failed");
        await Assert.That(text).Contains("example stack trace");
        await Assert.That(document.Descendants().Count(element => element.Name.LocalName == "UnitTestResult")).IsEqualTo(1);
    }

    [Test]
    public async Task Redacts_Compressed_Html_Payload_And_Preserves_Report()
    {
        var path = Path.Combine(_directory.FullName, "report.html");
        var payload = Encoding.UTF8.GetBytes($$"""{"Output":"license activation failed: {{Secret}}","StackTrace":"example stack trace","Status":"failed"}""");
        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            await gzip.WriteAsync(payload);
        }

        await File.WriteAllTextAsync(path, "<html><title>Test report</title><script id=\"report-data\" type=\"application/octet-stream\">"
            + Convert.ToBase64String(compressed.ToArray()) + "</script></html>");

        await ReportRedaction.RedactDirectoryAsync(_directory.FullName, Secret);

        var html = await File.ReadAllTextAsync(path);
        var encoded = html.Split("application/octet-stream\">", StringSplitOptions.None)[1].Split("</script>", StringSplitOptions.None)[0];
        await using var input = new MemoryStream(Convert.FromBase64String(encoded));
        await using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressor);
        var json = await reader.ReadToEndAsync();
        await Assert.That(html).Contains("<title>Test report</title>");
        await Assert.That(json).DoesNotContain(Secret);
        await Assert.That(json).Contains("license activation failed");
        await Assert.That(json).Contains("example stack trace");
        await Assert.That(JsonNode.Parse(json)!["Status"]!.GetValue<string>()).IsEqualTo("failed");
    }

    [Test]
    public async Task Does_Not_Remove_Activation_Diagnostics_From_Logs()
    {
        await Assert.That(ReportRedaction.Mask($"license activation failed: {Secret}", Secret))
            .IsEqualTo("license activation failed: [REDACTED]");
    }

    [Test]
    public async Task Redacts_Nested_Report_Data_Without_Dropping_Results()
    {
        var path = Path.Combine(_directory.FullName, "report.json");
        await File.WriteAllTextAsync(path, $$"""{"Groups":[{"Tests":[{"Output":"{{Secret}}","Message":"license activation failed","Duration":42}]}]}""");

        await ReportRedaction.RedactDirectoryAsync(_directory.FullName, Secret);

        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        var result = document["Groups"]![0]!["Tests"]![0]!;
        await Assert.That(result["Output"]!.GetValue<string>()).IsEqualTo("[REDACTED]");
        await Assert.That(result["Message"]!.GetValue<string>()).IsEqualTo("license activation failed");
        await Assert.That(result["Duration"]!.GetValue<int>()).IsEqualTo(42);
    }

    [Test]
    [Arguments("missing")]
    [Arguments("extra-script")]
    [Arguments("extra-id")]
    public async Task Rejects_Unknown_Html_Payload_Instead_Of_Publishing_It(string defect)
    {
        var html = defect == "missing" ? $"<html>{Secret}</html>" : await HtmlAsync("{}");
        html += defect switch
        {
            "extra-script" => "<script type='application/octet-stream'>unknown payload</script>",
            "extra-id" => "<script id='report-data'>unknown payload</script>",
            _ => "",
        };
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "report.html"), html);

        var failure = await Assert.That(() => ReportRedaction.RedactDirectoryAsync(_directory.FullName, Secret)).ThrowsExactly<InvalidDataException>();
        await Assert.That(failure!.Message).IsEqualTo("Report could not be safely checked: report.html");
    }

    [Test]
    [Arguments("runtime.trx")]
    [Arguments("profile-run.json")]
    [Arguments("legacy-image-one.json")]
    [Arguments("directory")]
    public async Task Rejects_Linked_Artifact_Paths(string name)
    {
        await WriteEvidenceAsync();
        var directory = _directory.FullName;
        if (name == "directory")
        {
            directory = Path.Combine(directory, "linked-evidence");
            Directory.CreateSymbolicLink(directory, _directory.FullName);
        }
        else
        {
            var path = Path.Combine(directory, name);
            File.Move(path, path + ".original");
            File.CreateSymbolicLink(path, path + ".original");
        }

        await Assert.That(async () => { await RuntimeEvidence.ValidateAsync(directory, "legacy", "revision", Manifest(), Images()); })
            .Throws<InvalidDataException>();
    }

    [Test]
    public async Task Cancellation_Before_Start_Does_Not_Launch_A_Command()
    {
        var info = ProcessRunner.Create("must-not-be-started", _directory.FullName, []);

        await Assert.That(async () =>
        {
            await ProcessRunner.RunAsync(info, null, null, echo: false, new CancellationToken(canceled: true));
        }).Throws<OperationCanceledException>();
    }

    [Test]
    [Arguments("manifest")]
    [Arguments("images")]
    [Arguments("profile-run.json")]
    [Arguments("legacy-image-one.json")]
    public async Task Rejects_Unknown_Schemas(string target)
    {
        await WriteEvidenceAsync();
        var manifest = Manifest();
        var images = Images();
        (target == "manifest" ? manifest : images)["schema_version"] = 2;
        if (target.EndsWith(".json", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(Path.Combine(_directory.FullName, target), (await File.ReadAllTextAsync(Path.Combine(_directory.FullName, target))).Replace("\"schema_version\":2", "\"schema_version\":1", StringComparison.Ordinal));
            await RefreshHashesAsync(_directory.FullName);
            images = Images();
        }

        await Assert.That(async () => { await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", manifest, images); })
            .Throws<InvalidDataException>();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Pair_Requires_Identical_Executed_Test_Sets(bool extraCurrentTest)
    {
        await WriteEvidenceAsync();
        var current = _directory.CreateSubdirectory("current").FullName;
        var legacy = _directory.CreateSubdirectory("legacy").FullName;
        foreach (var file in Directory.EnumerateFiles(_directory.FullName))
        {
            File.Copy(file, Path.Combine(current, Path.GetFileName(file)));
            File.Copy(file, Path.Combine(legacy, Path.GetFileName(file)));
        }

        var record = Path.Combine(current, "profile-run.json");
        await File.WriteAllTextAsync(record, (await File.ReadAllTextAsync(record)).Replace("legacy", "current", StringComparison.Ordinal));
        var currentRecord = JsonNode.Parse(await File.ReadAllTextAsync(record))!;
        currentRecord["execution_id"] = "execution-current";
        await File.WriteAllTextAsync(record, currentRecord.ToJsonString());
        var receipt = Path.Combine(current, "current-image-one.json");
        File.Move(Path.Combine(current, "legacy-image-one.json"), receipt);
        await File.WriteAllTextAsync(receipt, (await File.ReadAllTextAsync(receipt)).Replace("legacy", "current", StringComparison.Ordinal).Replace("execution-one", "execution-current", StringComparison.Ordinal));
        await RefreshHashesAsync(current);
        await RefreshHashesAsync(legacy);
        if (extraCurrentTest)
        {
            var report = XDocument.Parse(Report());
            var ns = report.Root!.Name.Namespace;
            report.Root.Element(ns + "TestDefinitions")!.Add(new XElement(ns + "UnitTest", new XAttribute("id", "two"),
                new XElement(ns + "TestMethod", new XAttribute("className", "Example.Tests"), new XAttribute("name", "Extra"))));
            report.Root.Element(ns + "Results")!.Add(new XElement(ns + "UnitTestResult", new XAttribute("testId", "two"),
                new XAttribute("testName", "Extra"), new XAttribute("outcome", "Passed")));
            var counters = report.Descendants(ns + "Counters").Single();
            counters.SetAttributeValue("total", 2);
            counters.SetAttributeValue("executed", 2);
            counters.SetAttributeValue("passed", 2);
            await File.WriteAllTextAsync(Path.Combine(current, "runtime.trx"), report.ToString());
            await RefreshHashesAsync(current);
            await Assert.That(async () => { await RuntimeEvidence.ValidatePairAsync(current, legacy, "revision", Manifest(), Images()); })
                .Throws<InvalidDataException>();
        }
        else
        {
            await Assert.That(await RuntimeEvidence.ValidatePairAsync(current, legacy, "revision", Manifest(), Images())).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments("blob.bin", "unsupported")]
    [Arguments("extra.html", "duplicate")]
    [Arguments("broken.json", "{sensitive-content")]
    [Arguments("broken.xml", "<sensitive-content")]
    [Arguments("unknown.html", "<html>sensitive-content</html>")]
    public async Task Rejects_Uncheckable_Reports_Before_Copy(string name, string content)
    {
        if (content == "duplicate")
        {
            content = await HtmlAsync("{}") + "<script id='report-data'>unrecognized payload</script>";
        }

        var failure = await Assert.That(() => RunFixtureAsync(async (info, _) =>
        {
            var reports = info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!;
            await File.WriteAllTextAsync(Path.Combine(reports, "safe.log"), "license activation failed: example stack trace", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(reports, Secret + name), content, CancellationToken.None);
            return (1, "");
        })).ThrowsExactly<InvalidDataException>();

        await Assert.That(failure!.Message).IsEqualTo($"Report could not be safely checked: [REDACTED]{name}");
        await Assert.That(Directory.GetFiles(Output()).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(["profile-run.json"], CollectionOrdering.Matching);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(_scratch.Single(), "results", "safe.log")))
            .IsEqualTo("license activation failed: example stack trace");
        var reports = Path.Combine(_scratch.Single(), "results");
        File.Move(Path.Combine(reports, Secret + name), Path.Combine(reports, name));
        var plainFailure = await Assert.That(() => ReportRedaction.RedactDirectoryAsync(reports, Secret))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(plainFailure!.Message).IsEqualTo($"Report could not be safely checked: {name}");
    }

    [Test]
    [Arguments("json", "raw")]
    [Arguments("json", "uri")]
    [Arguments("json", "escaped-key")]
    [Arguments("html", "raw")]
    [Arguments("html", "uri")]
    [Arguments("xml", "raw")]
    [Arguments("xml", "html")]
    [Arguments("xml", "uri")]
    [Arguments("values", "encoded")]
    public async Task Rejects_Residual_Tokens_After_Masking(string format, string form)
    {
        const string encodedSecret = "synthetic<& token";
        var secret = format is "xml" or "values" ? encodedSecret : "synthetic/report";
        if (form == "escaped-key")
        {
            secret = "synthetic+&'token";
        }
        var token = form switch { "html" => WebUtility.HtmlEncode(secret), "uri" => Uri.EscapeDataString(secret), _ => secret };
        var path = Path.Combine(_directory.FullName, "report." + (format == "values" ? "json" : format));
        var content = format switch
        {
            "json" => new JsonObject { [token] = "diagnostic" }.ToJsonString(),
            "html" => await HtmlAsync(new JsonObject { [token] = "diagnostic" }.ToJsonString()),
            "xml" => $"<report><!--{token}--></report>",
            _ => new JsonObject
            {
                ["Output"] = secret,
                ["Message"] = WebUtility.HtmlEncode(secret),
                ["StackTrace"] = Uri.EscapeDataString(secret),
                ["Status"] = "failed"
            }.ToJsonString(),
        };
        await File.WriteAllTextAsync(path, content);
        if (format == "values")
        {
            await ReportRedaction.RedactDirectoryAsync(_directory.FullName, secret);
            var result = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            foreach (var field in new[] { "Output", "Message", "StackTrace" })
            {
                await Assert.That(result[field]!.GetValue<string>()).IsEqualTo("[REDACTED]");
            }

            await Assert.That(result["Status"]!.GetValue<string>()).IsEqualTo("failed");
            var htmlPath = Path.Combine(_directory.FullName, "report.html");
            await File.WriteAllTextAsync(htmlPath, await HtmlAsync(content));
            await ReportRedaction.RedactDirectoryAsync(_directory.FullName, secret);
            var html = await File.ReadAllTextAsync(htmlPath);
            var payload = html.Split("application/octet-stream\">", StringSplitOptions.None)[1].Split("</script>", StringSplitOptions.None)[0];
            await using var input = new MemoryStream(Convert.FromBase64String(payload));
            await using var gzip = new GZipStream(input, CompressionMode.Decompress);
            var decoded = (await JsonNode.ParseAsync(gzip))!;
            foreach (var field in new[] { "Output", "Message", "StackTrace" })
            {
                await Assert.That(decoded[field]!.GetValue<string>()).IsEqualTo("[REDACTED]");
            }

            await Assert.That(decoded["Status"]!.GetValue<string>()).IsEqualTo("failed");
        }
        else
        {
            var failure = await Assert.That(() => ReportRedaction.RedactDirectoryAsync(_directory.FullName, secret))
                .ThrowsExactly<InvalidDataException>();
            await Assert.That(failure!.Message).IsEqualTo($"Report could not be safely checked: report.{format}");
        }
    }

    [Test]
    [Arguments("success")]
    [Arguments("nonzero")]
    [Arguments("cancelled")]
    [Arguments("process")]
    [Arguments("report")]
    public async Task Finalizes_Private_Summary_On_Every_Exit_Path(string outcome)
    {
        var destination = Path.Combine(_directory.FullName, "summary.md");
        await File.WriteAllTextAsync(destination, "existing-prefix\n");
        using var cancellation = new CancellationTokenSource();
        var primary = new IOException("process diagnostic");
        Func<Task<int>> run = () => RunFixtureAsync(async (info, token) =>
        {
            var source = info.Environment["GITHUB_STEP_SUMMARY"]!;
            await Assert.That(source).IsEqualTo(Path.Combine(_scratch.Single(), "summary.md"));
            await Assert.That(await File.ReadAllTextAsync(destination, token)).IsEqualTo("existing-prefix\n");
            await File.WriteAllTextAsync(source, $"contribution: {Secret}\n", token);
            if (outcome == "cancelled")
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
            }

            if (outcome == "process")
            {
                throw primary;
            }

            await EmitReportsAsync(info);
            if (outcome == "report")
            {
                await File.WriteAllTextAsync(Path.Combine(info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!, "bad.bin"), Secret, token);
            }

            return (outcome == "nonzero" ? 7 : 0, "");
        }, destination, cancellationToken: cancellation.Token);
        if (outcome is "success" or "nonzero")
        {
            await Assert.That(await run()).IsEqualTo(outcome == "success" ? 0 : 7);
        }
        else if (outcome == "process")
        {
            await Assert.That(await Assert.That(run).ThrowsExactly<IOException>()).IsSameReferenceAs(primary);
        }
        else if (outcome == "cancelled")
        {
            await Assert.That(run).ThrowsExactly<OperationCanceledException>();
        }
        else
        {
            var failure = await Assert.That(run).ThrowsExactly<InvalidDataException>();
            await Assert.That(failure!.Message).IsEqualTo("Report could not be safely checked: bad.bin");
        }

        await Assert.That(await File.ReadAllTextAsync(destination)).IsEqualTo("existing-prefix\ncontribution: [REDACTED]\n");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "profile-run.json")))!;
        await Assert.That(record["status"]!.GetValue<string>()).IsEqualTo(outcome switch
        { "success" => "passed", "cancelled" => "cancelled", _ => "failed" });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Summary_Masking_Failure_Appends_Nothing(bool processFailure)
    {
        var destination = Path.Combine(_directory.FullName, "summary.md");
        await File.WriteAllTextAsync(destination, "existing-prefix");
        var primary = new IOException("primary process failure");
        Func<Task<int>> run = () => RunFixtureAsync(async (info, _) =>
        {
            Directory.CreateDirectory(info.Environment["GITHUB_STEP_SUMMARY"]!);
            if (processFailure)
            {
                throw primary;
            }

            await EmitReportsAsync(info);
            return (0, "");
        }, destination);
        if (processFailure)
        {
            await Assert.That(await Assert.That(run).ThrowsExactly<IOException>()).IsSameReferenceAs(primary);
        }
        else
        {
            var failure = await Assert.That(run).ThrowsExactly<InvalidDataException>();
            await Assert.That(failure!.Message).IsEqualTo("Private runtime summary could not be safely finalized: summary.md");
        }

        await Assert.That(await File.ReadAllTextAsync(destination)).IsEqualTo("existing-prefix");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "profile-run.json")))!;
        await Assert.That(record["status"]!.GetValue<string>()).IsEqualTo("failed");
    }

    [Test]
    public async Task Finalizes_Summary_Without_A_Real_Destination()
    {
        var primary = new IOException("keep scratch for inspection");
        Func<Task<int>> run = () => RunFixtureAsync(async (info, _) =>
        {
            await File.WriteAllTextAsync(info.Environment["GITHUB_STEP_SUMMARY"]!, Secret, CancellationToken.None);
            throw primary;
        });
        await Assert.That(await Assert.That(run).ThrowsExactly<IOException>()).IsSameReferenceAs(primary);
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(_scratch.Single(), "summary.md"))).IsEqualTo("[REDACTED]");
        await Assert.That(Directory.GetFiles(_directory.FullName)).IsEmpty();
    }

    [Test]
    public async Task Runner_Records_Final_Artifact_Hashes_And_Execution_Id()
    {
        string? executionId = null;
        await Assert.That(await RunFixtureAsync(async (info, _) =>
        {
            executionId = info.Environment["LOCALSTACK_TEST_EXECUTION_ID"];
            await EmitReportsAsync(info);
            return (0, "");
        })).IsEqualTo(0);
        var record = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "profile-run.json")))!;
        await Assert.That(Guid.Parse(record["execution_id"]!.GetValue<string>()).ToString()).IsEqualTo(executionId);
        var relative = "nested/legacy-image-one.json";
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), relative)))!;
        await Assert.That(record["schema_version"]!.GetValue<int>()).IsEqualTo(2);
        await Assert.That(receipt["schema_version"]!.GetValue<int>()).IsEqualTo(2);
        await Assert.That(receipt["execution_id"]!.GetValue<string>()).IsEqualTo(executionId);
        await Assert.That(record["artifact_sha256"]!.AsObject().Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray())
            .IsEquivalentTo(new[] { relative, "runtime.trx" }.Order(StringComparer.Ordinal).ToArray(), CollectionOrdering.Matching);
        foreach (var path in new[] { relative, "runtime.trx" })
        {
            var digest = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(Output(), path))));
            await Assert.That(record["artifact_sha256"]![path]!.GetValue<string>()).IsEqualTo(digest);
        }

        var trx = XDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "runtime.trx")));
        await Assert.That(trx.Descendants().Single(element => element.Name.LocalName == "Message").Value)
            .IsEqualTo("license activation failed: [REDACTED]");
        await Assert.That(trx.Descendants().Single(element => element.Name.LocalName == "StackTrace").Value).IsEqualTo("example stack trace");
        await Assert.That(trx.Descendants().Single(element => element.Name.LocalName == "UnitTestResult").Attribute("testName")!.Value).IsEqualTo("Works");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Rejects_Unbound_Runtime_Evidence(bool receiptOnly)
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, "profile-run.json");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        if (!receiptOnly)
        {
            record.AsObject().Remove("execution_id");
            record.AsObject().Remove("artifact_sha256");
        }
        await File.WriteAllTextAsync(path, record.ToJsonString());
        var receiptPath = Path.Combine(_directory.FullName, "legacy-image-one.json");
        var receipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath))!;
        receipt.AsObject().Remove("execution_id");
        await File.WriteAllTextAsync(receiptPath, receipt.ToJsonString());
        if (receiptOnly)
        {
            await RefreshHashesAsync(_directory.FullName);
        }

        await AssertEvidenceFailureAsync(receiptOnly ? "Runtime receipt execution id is missing: legacy-image-one.json"
            : "Runtime metadata contains missing or unexpected fields.");
    }

    [Test]
    [Arguments("runtime.trx")]
    [Arguments("legacy-image-one.json")]
    public async Task Rejects_Artifact_Hash_Mismatches(string name)
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, name);
        await File.AppendAllTextAsync(path, "\n");
        await AssertEvidenceFailureAsync($"Runtime artifact hash mismatch: {name}");
    }

    [Test]
    public async Task Rejects_Receipt_Execution_Id_Mismatch()
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, "legacy-image-one.json");
        await File.WriteAllTextAsync(path, (await File.ReadAllTextAsync(path)).Replace("execution-one", "execution-other", StringComparison.Ordinal));
        await RefreshHashesAsync(_directory.FullName);
        await AssertEvidenceFailureAsync("Runtime receipt execution id mismatch: legacy-image-one.json");
    }

    [Test]
    [Arguments("missing")]
    [Arguments("extra")]
    [Arguments("receipt")]
    [Arguments("blank-id")]
    public async Task Rejects_Inexact_Artifact_Hash_Inventory(string defect)
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, "profile-run.json");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        if (defect == "missing")
        {
            record["artifact_sha256"]!.AsObject().Remove("runtime.trx");
        }
        if (defect == "extra")
        {
            record["artifact_sha256"]!["../outside.trx"] = "unused";
        }
        if (defect == "blank-id")
        {
            record["execution_id"] = " ";
        }
        if (defect == "receipt")
        {
            File.Copy(Path.Combine(_directory.FullName, "legacy-image-one.json"), Path.Combine(_directory.FullName, "legacy-image-extra.json"));
        }
        await File.WriteAllTextAsync(path, record.ToJsonString());
        await AssertEvidenceFailureAsync(defect == "blank-id" ? "A nonblank runtime execution id is required."
            : "Runtime artifact hash inventory does not match the report and receipts.");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Pump_Fault_Kills_And_Reaps_Before_Rethrow(bool stdout)
        => await AssertPumpFailureAsync(stdout, secondaryFailure: false);

    [Test]
    public async Task Pump_Fault_Is_Not_Hidden_By_Another_Pending_Pump()
        => await AssertPumpFailureAsync(stdout: true, secondaryFailure: true);

    private static async Task AssertPumpFailureAsync(bool stdout, bool secondaryFailure)
    {
        var exit = new TaskCompletionSource();
        var reaped = new TaskCompletionSource();
        var sibling = new TaskCompletionSource<string?>();
        var events = new List<string>();
        var primary = new IOException("reader failed");
        var readSibling = Substitute.For<Func<Task<string?>>>();
        readSibling().Returns(sibling.Task);
        var waitForExit = Substitute.For<Func<CancellationToken, Task>>();
        waitForExit(Arg.Any<CancellationToken>()).Returns(exit.Task);
        using var cancellation = new CancellationTokenSource();
        Task<string?> Fault() => Task.FromException<string?>(primary);
        async Task<string?> Other()
        {
            try
            {
                return await readSibling();
            }
            finally
            {
                events.Add("sibling");
            }
        }

        var run = ProcessRunner.RunAsync(() => { }, token => WaitForReapAsync(waitForExit, events, reaped, secondaryFailure, token), () => events.Add("kill"), () => false, () => 0,
            stdout ? Fault : Other, stdout ? Other : Fault, TextWriter.Null, Secret, echo: false, cancellation.Token);
        var observeRun = Substitute.For<Func<Task>>();
        observeRun().Returns(run);
        try
        {
            await Assert.That(events.ToArray()).IsEquivalentTo(["kill"], CollectionOrdering.Matching);
            await Assert.That(run.IsCompleted).IsFalse();
            exit.SetResult();
            await reaped.Task;
            await Assert.That(run.IsCompleted).IsFalse();
            if (secondaryFailure)
            {
                sibling.SetException(new IOException("sibling failed"));
            }
            else
            {
                sibling.SetResult(null);
            }
            IOException? failure = null;
            try
            {
                await run;
            }
            catch (IOException exception)
            {
                failure = exception;
            }
            events.Add("rethrown");
            await Assert.That(failure).IsSameReferenceAs(primary);
            await Assert.That(events.ToArray()).IsEquivalentTo(["kill", "reaped", "sibling", "rethrown"], CollectionOrdering.Matching);
        }
        finally
        {
            exit.TrySetResult();
            sibling.TrySetResult(null);
            await Assert.That(await Assert.That(observeRun).ThrowsExactly<IOException>()).IsSameReferenceAs(primary);
        }
    }

    private static async Task WaitForReapAsync(Func<CancellationToken, Task> waitForExit, List<string> events,
        TaskCompletionSource reaped, bool secondaryFailure, CancellationToken token)
    {
        await waitForExit(token);
        if (!token.CanBeCanceled)
        {
            events.Add("reaped");
            reaped.SetResult();
            if (secondaryFailure)
            {
                throw new IOException("reap failed");
            }
        }
    }

    [Test]
    public async Task Successful_Pump_Completion_Keeps_Waiting()
    {
        var exit = new TaskCompletionSource();
        var error = new TaskCompletionSource<string?>();
        var killed = false;
        var lines = new Queue<string?>(["stdout " + Secret, null]);
        var waitForExit = Substitute.For<Func<CancellationToken, Task>>();
        waitForExit(Arg.Any<CancellationToken>()).Returns(exit.Task);
        var readError = Substitute.For<Func<Task<string?>>>();
        readError().Returns(error.Task, Task.FromResult<string?>(null));
        var run = ProcessRunner.RunAsync(() => { }, waitForExit, () => killed = true, () => false, () => 7,
            () => Task.FromResult(lines.Dequeue()), readError,
            TextWriter.Null, Secret, echo: false, CancellationToken.None);
        try
        {
            await Assert.That(killed).IsFalse();
            await Assert.That(run.IsCompleted).IsFalse();
            error.SetResult("stderr " + Secret);
            exit.SetResult();
            var result = await run;
            await Assert.That(result.ExitCode).IsEqualTo(7);
            await Assert.That(result.Output).IsEqualTo($"stdout [REDACTED]{Environment.NewLine}stderr [REDACTED]{Environment.NewLine}");
            await Assert.That(killed).IsFalse();
        }
        finally
        {
            error.TrySetResult(null);
            exit.TrySetResult();
            await run;
        }
    }

    private string Output() => Path.Combine(_directory.FullName, "output");

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Rejects_Secret_Report_Names_Before_Copy(bool folder)
    {
        var failure = await Assert.That(() => RunFixtureAsync(async (info, _) =>
        {
            var path = Path.Combine(info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!, folder ? Secret : Secret + ".log");
            if (folder)
            {
                Directory.CreateDirectory(path);
                path = Path.Combine(path, "harmless.log");
            }

            await File.WriteAllTextAsync(path, "harmless", CancellationToken.None);
            return (1, "");
        })).ThrowsExactly<InvalidDataException>();
        await Assert.That(failure!.Message).IsEqualTo(folder
            ? "Report could not be safely checked: [REDACTED]" : "Report could not be safely checked: [REDACTED].log");
        await Assert.That(Directory.GetFiles(Output()).Select(path => Path.GetFileName(path)).ToArray())
            .IsEquivalentTo(["profile-run.json"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task Validation_Failure_Preserves_Masked_Reports()
    {
        var failure = await Assert.That(() => RunFixtureAsync(async (info, _) =>
        {
            await File.WriteAllTextAsync(Path.Combine(info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!, "runtime.trx"), Report(), CancellationToken.None);
            return (0, "");
        })).ThrowsExactly<InvalidDataException>();
        await Assert.That(failure!.Message).IsEqualTo("Required host image evidence is missing or unexpected.");
        await Assert.That(File.Exists(Path.Combine(Output(), "runtime.trx"))).IsTrue();
        var trx = XDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "runtime.trx")));
        await Assert.That(trx.Descendants().Single(element => element.Name.LocalName == "Message").Value)
            .IsEqualTo("license activation failed: [REDACTED]");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "profile-run.json")))!;
        await Assert.That(record["status"]!.GetValue<string>()).IsEqualTo("failed");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task Secret_Free_Reports_Are_Copied_Byte_Identical(string? secret)
    {
        var source = _directory.CreateSubdirectory("reports").FullName;
        foreach (var name in new[] { "unsupported.bin", "runtime.trx" })
        {
            await File.WriteAllTextAsync(Path.Combine(source, name), name == "runtime.trx" ? Report() : "unsupported content\r\n");
        }

        await ReportRedaction.RedactDirectoryAsync(source, secret);
        ProcessRunner.CopyDirectory(source, Output());
        foreach (var name in new[] { "unsupported.bin", "runtime.trx" })
        {
            await Assert.That(await File.ReadAllBytesAsync(Path.Combine(Output(), name)))
                .IsEquivalentTo(await File.ReadAllBytesAsync(Path.Combine(source, name)), CollectionOrdering.Matching);
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(Output(), name)))
                .IsEqualTo(name == "runtime.trx" ? Report() : "unsupported content\r\n");
        }
    }

    [Test]
    public async Task Scratch_Delete_Failure_Records_Failure()
    {
        var failure = await Assert.That(() => RunFixtureAsync(async (info, _) =>
        {
            await EmitReportsAsync(info);
            return (0, "");
        }, git: async args =>
        {
            if (_scratch.Count > 0 && args[0] == "status")
            {
                Directory.Delete(_scratch.Single(), recursive: true);
                await File.WriteAllTextAsync(_scratch.Single(), "block directory deletion", CancellationToken.None);
            }

            return args[0] == "rev-parse" ? "revision" : "";
        })).ThrowsExactly<IOException>();
        File.Delete(_scratch.Single());
        await Assert.That(failure!.Message).IsEqualTo("Runtime scratch cleanup failed.");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Output(), "profile-run.json")))!;
        await Assert.That(record["status"]!.GetValue<string>()).IsEqualTo("failed");
    }

    [Test]
    public async Task Mid_Run_Cancellation_Kills_Reaps_And_Observes_Both_Pumps()
    {
        using var cancellation = new CancellationTokenSource();
        var exit = new TaskCompletionSource();
        var reaped = new TaskCompletionSource();
        var output = new TaskCompletionSource<string?>();
        var error = new TaskCompletionSource<string?>();
        var waiting = new TaskCompletionSource();
        var primary = new OperationCanceledException(cancellation.Token);
        using var registration = cancellation.Token.Register(() => waiting.SetException(primary));
        var outputObserved = new TaskCompletionSource();
        var events = new List<string>();
        var readOutput = Substitute.For<Func<Task<string?>>>();
        readOutput().Returns(output.Task);
        var readError = Substitute.For<Func<Task<string?>>>();
        readError().Returns(error.Task);
        var wait = Substitute.For<Func<CancellationToken, Task>>();
        wait(cancellation.Token).Returns(waiting.Task);
        var awaitExit = Substitute.For<Func<Task>>();
        awaitExit().Returns(exit.Task);
        wait(CancellationToken.None).Returns(_ => ReapAsync());
        async Task ReapAsync()
        {
            await awaitExit();
            events.Add("reaped");
            reaped.SetResult();
        }

        async Task<string?> ReadAsync(Func<Task<string?>> read, string name)
        {
            var line = await read();
            events.Add(name);
            if (name == "stdout")
            {
                outputObserved.SetResult();
            }

            return line;
        }

        var run = ProcessRunner.RunAsync(() => { }, wait, () => events.Add("kill"), () => false, () => 0,
            () => ReadAsync(readOutput, "stdout"), () => ReadAsync(readError, "stderr"), TextWriter.Null, Secret, false, cancellation.Token);
        var observeRun = Substitute.For<Func<Task>>();
        observeRun().Returns(run);
        await cancellation.CancelAsync();
        try
        {
            await Assert.That(events.ToArray()).IsEquivalentTo(["kill"], CollectionOrdering.Matching);
            exit.SetResult();
            await reaped.Task;
            output.SetResult(null);
            await outputObserved.Task;
            error.SetResult(null);
            await Assert.That(await Assert.That(observeRun).ThrowsExactly<OperationCanceledException>()).IsSameReferenceAs(primary);
            events.Add("rethrown");
            await Assert.That(events.ToArray()).IsEquivalentTo(["kill", "reaped", "stdout", "stderr", "rethrown"], CollectionOrdering.Matching);
        }
        finally
        {
            exit.TrySetResult();
            output.TrySetResult(null);
            error.TrySetResult(null);
            await Assert.That(await Assert.That(observeRun).ThrowsExactly<OperationCanceledException>()).IsSameReferenceAs(primary);
        }
    }

    private async Task<int> RunFixtureAsync(Func<ProcessStartInfo, CancellationToken, Task<(int, string)>> execute,
        string? destination = null, Func<string[], Task<string>>? git = null, CancellationToken cancellationToken = default)
    {
        var root = _directory.CreateSubdirectory("root").FullName;
        Directory.CreateDirectory(Path.Combine(root, ".github/scripts"));
        Directory.CreateDirectory(Path.Combine(root, "tests/Aspire.Hosting.LocalStack.Integration.Tests/TestInfrastructure"));
        await File.WriteAllTextAsync(Path.Combine(root, ".github/scripts/common_runtime_tests.json"), Manifest().ToJsonString(), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "tests/Aspire.Hosting.LocalStack.Integration.Tests/TestInfrastructure/runtime-images.json"), Images().ToJsonString(), cancellationToken);
        return await RuntimeRunner.RunAsync(root, "legacy", Output(), Secret, destination, (info, token) =>
        {
            _scratch.Add(Path.GetDirectoryName(info.Environment["GITHUB_STEP_SUMMARY"]!)!);
            return execute(info, token);
        }, git ?? (args => Task.FromResult(args[0] == "rev-parse" ? "revision" : "")), cancellationToken);
    }

    private static async Task EmitReportsAsync(ProcessStartInfo info)
    {
        var reports = info.Environment["LOCALSTACK_TEST_RESULTS_DIRECTORY"]!;
        await File.WriteAllTextAsync(Path.Combine(reports, "runtime.trx"), Report());
        Directory.CreateDirectory(Path.Combine(reports, "nested"));
        var receipt = JsonNode.Parse("""
            {"schema_version":2,"runtime_id":"legacy","scenario_group":"lambda","image":{"registry":"docker.io","image":"localstack/localstack","tag":"legacy-tag","digest":null}}
            """)!;
        receipt["execution_id"] = info.Environment["LOCALSTACK_TEST_EXECUTION_ID"];
        await File.WriteAllTextAsync(Path.Combine(reports, "nested", "legacy-image-one.json"), receipt.ToJsonString());
    }

    private async Task AssertEvidenceFailureAsync(string message)
    {
        var failure = await Assert.That(async () => await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", Manifest(), Images()))
            .ThrowsExactly<InvalidDataException>();
        await Assert.That(failure!.Message).IsEqualTo(message);
    }

    private static async Task RefreshHashesAsync(string directory)
    {
        var path = Path.Combine(directory, "profile-run.json");
        var record = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        var hashes = new JsonObject();
        foreach (var artifact in Directory.EnumerateFiles(directory, "*-image-*.json", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(directory, "runtime.trx")))
        {
            hashes[Path.GetRelativePath(directory, artifact).Replace('\\', '/')] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(artifact)));
        }

        record["artifact_sha256"] = hashes;
        await File.WriteAllTextAsync(path, record.ToJsonString());
    }

    private static async Task<string> HtmlAsync(string json)
    {
        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(json));
        }

        return "<html><title>Test report</title><script id=\"report-data\" type=\"application/octet-stream\">"
            + Convert.ToBase64String(compressed.ToArray()) + "</script></html>";
    }

    public void Dispose()
    {
        foreach (var path in _scratch.Where(Directory.Exists))
        {
            Directory.Delete(path, recursive: true);
        }

        _directory.Delete(recursive: true);
    }

    private async Task WriteEvidenceAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "runtime.trx"), Report());
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "profile-run.json"), """
            {"schema_version":2,"runtime_id":"legacy","revision":"revision","source_clean":true,"configuration":"Release/net10.0","status":"passed","execution_id":"execution-one","artifact_sha256":{}}
            """);
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "legacy-image-one.json"), """
            {"schema_version":2,"runtime_id":"legacy","scenario_group":"lambda","execution_id":"execution-one","image":{"registry":"docker.io","image":"localstack/localstack","tag":"legacy-tag","digest":null}}
            """);
        await RefreshHashesAsync(_directory.FullName);
    }

    private static JsonNode Manifest() => JsonNode.Parse("""
        {"schema_version":1,"scenarios":[{"class":"Example.Tests","method":"Works","case":null}],"scenario_groups":["lambda"]}
        """)!;

    private static JsonNode Images() => JsonNode.Parse("""
        {"schema_version":1,"images":{"legacy":{"registry":"docker.io","image":"localstack/localstack","tag":"legacy-tag","digest":null},"current":{"registry":"docker.io","image":"localstack/localstack","tag":"current-tag","digest":null}}}
        """)!;

    private static string Report() => $$"""
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions><UnitTest id="one"><TestMethod className="Example.Tests" name="Works" /></UnitTest></TestDefinitions>
          <Results><UnitTestResult testId="one" testName="Works" outcome="Passed"><Output><ErrorInfo><Message>license activation failed: {{Secret}}</Message><StackTrace>example stack trace</StackTrace></ErrorInfo></Output></UnitTestResult></Results>
          <ResultSummary><Counters total="1" executed="1" passed="1" failed="0" notExecuted="0" /></ResultSummary>
        </TestRun>
        """;
}
