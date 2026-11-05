using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using global::LocalStack.Tools;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Tooling;

public sealed class RuntimeToolingTests : IDisposable
{
    private const string Secret = "synthetic-report-secret";
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("localstack-tools-tests-");

    [Test]
    public async Task Accepts_Complete_Runtime_Evidence()
    {
        await WriteEvidenceAsync();

        var observed = await RuntimeEvidence.ValidateAsync(_directory.FullName, "legacy", "revision", Manifest(), Images());

        await Assert.That(observed.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("Failed")]
    [Arguments("NotExecuted")]
    public async Task Rejects_Nonpassing_Outcomes(string outcome)
    {
        await WriteEvidenceAsync();
        var path = Path.Combine(_directory.FullName, "runtime.trx");
        await File.WriteAllTextAsync(path, Report().Replace("outcome=\"Passed\"", $"outcome=\"{outcome}\"", StringComparison.Ordinal));

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
    public async Task Rejects_Unknown_Html_Payload_Instead_Of_Publishing_It()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "report.html"), $"<html>{Secret}</html>");

        await Assert.That(() => ReportRedaction.RedactDirectoryAsync(_directory.FullName, Secret)).Throws<InvalidDataException>();
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
    public async Task Rejects_Unknown_Schemas(string target)
    {
        await WriteEvidenceAsync();
        var manifest = Manifest();
        var images = Images();
        (target == "manifest" ? manifest : images)["schema_version"] = 2;

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
        var receipt = Path.Combine(current, "current-image-one.json");
        File.Move(Path.Combine(current, "legacy-image-one.json"), receipt);
        await File.WriteAllTextAsync(receipt, (await File.ReadAllTextAsync(receipt)).Replace("legacy", "current", StringComparison.Ordinal));
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
            await Assert.That(async () => { await RuntimeEvidence.ValidatePairAsync(current, legacy, "revision", Manifest(), Images()); })
                .Throws<InvalidDataException>();
        }
        else
        {
            await Assert.That(await RuntimeEvidence.ValidatePairAsync(current, legacy, "revision", Manifest(), Images())).IsEqualTo(1);
        }
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private async Task WriteEvidenceAsync()
    {
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "runtime.trx"), Report());
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "profile-run.json"), """
            {"schema_version":1,"runtime_id":"legacy","revision":"revision","source_clean":true,"configuration":"Release/net10.0","status":"passed"}
            """);
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "legacy-image-one.json"), """
            {"schema_version":1,"runtime_id":"legacy","scenario_group":"lambda","image":{"registry":"docker.io","image":"localstack/localstack","tag":"legacy-tag","digest":null}}
            """);
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
