using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace LocalStack.Tools;

internal static class RuntimeEvidence
{
    private static readonly string[] CounterNames = ["total", "executed", "passed"];
    private static readonly string[] ImageFields = ["registry", "image", "tag", "digest"];
    private static readonly string[] RunFields = ["schema_version", "runtime_id", "revision", "source_clean", "configuration", "status", "execution_id", "artifact_sha256"];
    private static readonly string[] ReceiptFields = ["schema_version", "runtime_id", "scenario_group", "image", "execution_id"];

    internal static async Task<HashSet<(string Class, string Method, string? Case)>> ValidateAsync(
        string directory, string runtime, string revision, JsonNode manifest, JsonNode images, bool requireClean = true)
    {
        if (!Directory.Exists(directory) || File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("The runtime evidence directory is missing or linked.");
        }

        var run = await ReadAsync(Path.Combine(directory, "profile-run.json")).ConfigureAwait(false);
        RequireFields(run, RunFields);
        if (run["schema_version"]?.GetValue<int>() != 2 || run["runtime_id"]?.GetValue<string>() != runtime
            || run["revision"]?.GetValue<string>() != revision || run["status"]?.GetValue<string>() != "passed"
            || run["configuration"]?.GetValue<string>() != "Release/net10.0"
            || (requireClean && run["source_clean"]?.GetValue<bool>() != true))
        {
            throw new InvalidDataException("A passing, matching runtime execution record is required; release evidence must be clean.");
        }

        return await ValidateResultsAsync(directory, runtime, manifest, images, run).ConfigureAwait(false);
    }

    internal static async Task<HashSet<(string Class, string Method, string? Case)>> ValidateResultsAsync(
        string directory, string runtime, JsonNode manifest, JsonNode images, JsonNode run)
    {
        var executionId = Text(run, "execution_id");
        if (string.IsNullOrWhiteSpace(executionId))
        {
            throw new InvalidDataException("A nonblank runtime execution id is required.");
        }

        await ValidateHashesAsync(directory, run).ConfigureAwait(false);

        if (manifest["schema_version"]?.GetValue<int>() != 1 || images["schema_version"]?.GetValue<int>() != 1)
        {
            throw new InvalidDataException("Unsupported runtime evidence schema.");
        }

        var required = manifest["scenarios"]?.AsArray()
            .Select(row => (Text(row, "class"), Text(row, "method"), row?["case"]?.GetValue<string>())).ToHashSet()
            ?? throw new InvalidDataException("The required scenario manifest is missing.");
        if (required.Count == 0 || required.Count != manifest["scenarios"]!.AsArray().Count)
        {
            throw new InvalidDataException("The required scenario list must be nonempty and unique.");
        }

        var observed = await ReadReportAsync(Path.Combine(directory, "runtime.trx")).ConfigureAwait(false);
        if (!required.IsSubsetOf(observed))
        {
            throw new InvalidDataException("Required common scenarios are missing from the native test report.");
        }

        await ValidateImagesAsync(directory, runtime, manifest, images, executionId).ConfigureAwait(false);
        return observed;
    }

    private static async Task ValidateHashesAsync(string directory, JsonNode run)
    {
        RequireRegularFile(Path.Combine(directory, "runtime.trx"));
        var artifacts = Directory.EnumerateFiles(directory, "*-image-*.json", SearchOption.AllDirectories)
            .Append(Path.Combine(directory, "runtime.trx")).ToArray();
        if (run["artifact_sha256"] is not JsonObject hashes || hashes.Count != artifacts.Length
            || artifacts.Any(path => !hashes.ContainsKey(Path.GetRelativePath(directory, path).Replace('\\', '/'))))
        {
            throw new InvalidDataException("Runtime artifact hash inventory does not match the report and receipts.");
        }

        foreach (var path in artifacts)
        {
            RequireRegularFile(path);
            var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            var digest = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path).ConfigureAwait(false)));
            if (hashes[relative]?.GetValue<string>() != digest)
            {
                throw new InvalidDataException($"Runtime artifact hash mismatch: {relative}");
            }
        }
    }

    internal static async Task<int> ValidatePairAsync(string current, string legacy, string revision, JsonNode manifest, JsonNode images)
    {
        var first = await ValidateAsync(current, "current", revision, manifest, images).ConfigureAwait(false);
        var second = await ValidateAsync(legacy, "legacy", revision, manifest, images).ConfigureAwait(false);
        if (!first.SetEquals(second))
        {
            throw new InvalidDataException("The current and legacy runs executed different test sets.");
        }

        return first.Count;
    }

    internal static async Task<JsonNode> ReadAsync(string path)
    {
        RequireRegularFile(path);
        return JsonNode.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false))
            ?? throw new InvalidDataException("An evidence document is empty.");
    }

    private static async Task<HashSet<(string Class, string Method, string? Case)>> ReadReportAsync(string path)
    {
        RequireRegularFile(path);
        var root = XDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false)).Root
            ?? throw new InvalidDataException("The native TRX report is empty.");
        var trx = root.Name.Namespace;
        var definitions = root.Element(trx + "TestDefinitions")?.Elements(trx + "UnitTest")
            .ToDictionary(item => Attribute(item, "id"), item => item.Element(trx + "TestMethod"), StringComparer.Ordinal)
            ?? throw new InvalidDataException("Native test definitions are missing.");
        var observed = new HashSet<(string Class, string Method, string? Case)>();
        foreach (var result in root.Element(trx + "Results")?.Elements(trx + "UnitTestResult") ?? [])
        {
            if (Attribute(result, "outcome") != "Passed"
                || !definitions.TryGetValue(Attribute(result, "testId"), out var definition) || definition is null)
            {
                throw new InvalidDataException("Every integration test must execute and pass; inspect the preserved native diagnostics.");
            }

            var method = Attribute(definition, "name");
            var display = Attribute(result, "testName");
            if (!observed.Add((Attribute(definition, "className"), method, display == method ? null : display)))
            {
                throw new InvalidDataException("Duplicate native test outcomes were recorded.");
            }
        }

        var counters = root.Element(trx + "ResultSummary")?.Element(trx + "Counters")
            ?? throw new InvalidDataException("Native result counters are missing.");
        if (observed.Count == 0 || CounterNames
            .Any(key => Counter(Attribute(counters, key)) != observed.Count)
            || counters.Attributes().Any(item => item.Name.LocalName is not ("total" or "executed" or "passed")
                && Counter(item.Value) != 0))
        {
            throw new InvalidDataException("Native outcomes are zero, skipped, failed or incomplete.");
        }

        return observed;
    }

    private static async Task ValidateImagesAsync(string directory, string runtime, JsonNode manifest, JsonNode images, string executionId)
    {
        var expected = images["images"]?[runtime] ?? throw new InvalidDataException("The qualified runtime image is missing.");
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*-image-*.json", SearchOption.AllDirectories))
        {
            var receipt = await ReadAsync(path).ConfigureAwait(false);
            if (receipt["execution_id"] is null)
            {
                throw new InvalidDataException($"Runtime receipt execution id is missing: {Path.GetRelativePath(directory, path)}");
            }

            RequireFields(receipt, ReceiptFields);
            RequireFields(receipt["image"], ImageFields);
            if (Text(receipt, "execution_id") != executionId)
            {
                throw new InvalidDataException($"Runtime receipt execution id mismatch: {Path.GetRelativePath(directory, path)}");
            }

            if (receipt["schema_version"]?.GetValue<int>() != 2 || Text(receipt, "runtime_id") != runtime
                || !groups.Add(Text(receipt, "scenario_group"))
                || ImageFields.Any(key => !JsonNode.DeepEquals(receipt["image"]?[key], expected[key])))
            {
                throw new InvalidDataException("A host selected an unexpected image or emitted duplicate image evidence.");
            }
        }

        var required = manifest["scenario_groups"]?.AsArray().Select(node => node!.GetValue<string>())
            ?? throw new InvalidDataException("The expected host groups are missing.");
        if (!groups.SetEquals(required))
        {
            throw new InvalidDataException("Required host image evidence is missing or unexpected.");
        }
    }

    private static string Text(JsonNode? node, string key)
        => node?[key]?.GetValue<string>() ?? throw new InvalidDataException($"Required metadata is missing: {key}");

    private static string Attribute(XElement element, string name)
        => element.Attribute(name)?.Value ?? throw new InvalidDataException($"Required native metadata is missing: {name}");

    private static int Counter(string value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            ? count : throw new InvalidDataException("Native result counters are not nonnegative integers.");

    private static void RequireRegularFile(string path)
    {
        if (!File.Exists(path) || File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException($"Required evidence is missing or linked: {Path.GetFileName(path)}");
        }
    }

    private static void RequireFields(JsonNode? node, string[] fields)
    {
        if (node is not JsonObject obj || obj.Count != fields.Length || fields.Any(field => !obj.ContainsKey(field)))
        {
            throw new InvalidDataException("Runtime metadata contains missing or unexpected fields.");
        }
    }
}
