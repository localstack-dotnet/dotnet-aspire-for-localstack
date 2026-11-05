using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace LocalStack.Tools;

internal static class ConsumerVerification
{
    private const string PackageId = "LocalStack.Aspire.Hosting";
    private const string Library = "Aspire.Hosting.LocalStack.dll";
    private const string Caller = "LocalStack.LegacyConsumer.dll";
    private static readonly string[] FixtureFiles = ["global.json", "LocalStack.LegacyConsumer.csproj", "Program.cs"];
    private static readonly string[] ConsumerProperties =
    [
        "-p:ImportDirectoryBuildProps=false", "-p:ImportDirectoryBuildTargets=false", "-p:ManagePackageVersionsCentrally=false",
    ];

    internal static async Task<int> RunAsync(string root, string packages, string outputPath, CancellationToken cancellationToken)
    {
        var (archive, version) = await FindPackageAsync(packages, cancellationToken).ConfigureAwait(false);
        var output = ToolApp.NewOutput(outputPath);
        var scratch = Directory.CreateTempSubdirectory("localstack-consumer-");
        try
        {
            var feed = Path.Combine(scratch.FullName, "feed");
            Directory.CreateDirectory(feed);
            File.Copy(archive, Path.Combine(feed, Path.GetFileName(archive)));
            var baseline = await BuildAsync(root, scratch.FullName, output, "baseline", "13.4.0", null, cancellationToken).ConfigureAwait(false);
            var candidate = await BuildAsync(root, scratch.FullName, output, "candidate", version, feed, cancellationToken).ConfigureAwait(false);
            var cached = Path.Combine(scratch.FullName, "candidate-packages", PackageId.ToLowerInvariant(), version.ToLowerInvariant());
            var checksum = Path.Combine(cached, $"{PackageId.ToLowerInvariant()}.{version.ToLowerInvariant()}.nupkg.sha512");
            var saved = await VerifyHashControlsAsync(archive, checksum, cancellationToken).ConfigureAwait(false);
            var bytes = await ReadEntryAsync(archive, "lib/net8.0/" + Library, cancellationToken).ConfigureAwait(false);
            var expected = Convert.ToHexString(SHA256.HashData(bytes));
            if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(candidate, Library), cancellationToken).ConfigureAwait(false))) != expected)
            {
                throw new InvalidDataException("The candidate output contains a different library assembly.");
            }

            await RunCallerAsync(candidate, expected, Path.Combine(output, "source-run.log"), cancellationToken).ConfigureAwait(false);
            var staging = Path.Combine(scratch.FullName, "old-binary-on-candidate");
            ProcessRunner.CopyDirectory(candidate, staging);
            File.Copy(Path.Combine(baseline, Caller), Path.Combine(staging, Caller), overwrite: true);
            await RunCallerAsync(staging, expected, Path.Combine(output, "binary-run.log"), cancellationToken).ConfigureAwait(false);
            var missing = Path.Combine(staging, Library);
            var retained = missing + ".retained";
            File.Move(missing, retained);
            try
            {
                using var missingTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                missingTimeout.CancelAfter(TimeSpan.FromSeconds(30));
                var result = await ProcessRunner.RunAsync(ProcessRunner.Create("dotnet", staging, [Caller, expected]), null,
                    Path.Combine(output, "missing-assembly-negative.log"), echo: true, missingTimeout.Token).ConfigureAwait(false);
                if (result.ExitCode == 0 || result.Output.Contains("ABI_OK", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The missing-candidate negative control unexpectedly passed.");
                }
            }
            finally
            {
                File.Move(retained, missing);
            }

            var evidence = new JsonObject
            {
                ["baseline_package"] = "13.4.0",
                ["candidate_package"] = version,
                ["sdk_family"] = "8.0",
                ["language"] = "C#12",
                ["package_sha512_verified"] = true,
                ["source_consumer"] = "passed",
                ["old_binary_candidate_graph"] = "passed",
                ["candidate_package_sha512"] = saved.Trim(),
                ["loaded_library_sha256"] = expected,
                ["package_hash_negative"] = true,
                ["missing_candidate_negative"] = true,
            };
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), evidence.ToJsonString(), cancellationToken).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    internal static async Task VerifyPackageHashAsync(string archive, string checksum, CancellationToken cancellationToken)
    {
        var expected = Convert.ToBase64String(SHA512.HashData(await File.ReadAllBytesAsync(archive, cancellationToken).ConfigureAwait(false)));
        if ((await File.ReadAllTextAsync(checksum, cancellationToken).ConfigureAwait(false)).Trim() != expected)
        {
            throw new InvalidDataException("The restored candidate differs from the supplied package archive.");
        }
    }

    private static async Task<string> VerifyHashControlsAsync(string archive, string checksum, CancellationToken cancellationToken)
    {
        await VerifyPackageHashAsync(archive, checksum, cancellationToken).ConfigureAwait(false);
        var saved = await File.ReadAllTextAsync(checksum, cancellationToken).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(checksum, "deliberately-invalid-checksum", cancellationToken).ConfigureAwait(false);
            try
            {
                await VerifyPackageHashAsync(archive, checksum, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException)
            {
                return saved;
            }

            throw new InvalidDataException("The package-checksum negative control unexpectedly passed.");
        }
        finally
        {
            await File.WriteAllTextAsync(checksum, saved, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<(string Path, string Version)> FindPackageAsync(string directory, CancellationToken cancellationToken)
    {
        var matches = new List<(string Path, string Version)>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.nupkg"))
        {
            using var package = ZipFile.OpenRead(path);
            var spec = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
            var stream = spec.Open();
            await using var streamLifetime = stream.ConfigureAwait(false);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
            var metadata = document.Root?.Elements().Single(element => element.Name.LocalName == "metadata");
            if (metadata?.Elements().Single(element => element.Name.LocalName == "id").Value == PackageId)
            {
                matches.Add((Path.GetFullPath(path), metadata.Elements().Single(element => element.Name.LocalName == "version").Value));
            }
        }

        return matches.Count == 1 ? matches[0] : throw new InvalidDataException("Exactly one LocalStack.Aspire.Hosting candidate archive is required.");
    }

    private static async Task<byte[]> ReadEntryAsync(string archive, string name, CancellationToken cancellationToken)
    {
        using var package = ZipFile.OpenRead(archive);
        var entry = package.GetEntry(name) ?? throw new InvalidDataException("The packed net8 library is missing.");
        var stream = entry.Open();
        await using var streamLifetime = stream.ConfigureAwait(false);
        var buffer = new MemoryStream();
        await using var bufferLifetime = buffer.ConfigureAwait(false);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async Task<string> BuildAsync(string root, string scratch, string output, string name,
        string version, string? feed, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(scratch, name);
        Directory.CreateDirectory(directory);
        foreach (var file in FixtureFiles)
        {
            File.Copy(Path.Combine(root, "tests/compatibility/LocalStack.LegacyConsumer", file), Path.Combine(directory, file));
        }

        var sources = new XElement("packageSources", new XElement("clear"));
        if (feed is not null)
        {
            sources.Add(new XElement("add", new XAttribute("key", "candidate"), new XAttribute("value", feed)));
        }

        sources.Add(new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json")));
        var configuration = Path.Combine(directory, "NuGet.Config");
        await File.WriteAllTextAsync(configuration, new XDocument(new XElement("configuration", sources)).ToString(), cancellationToken).ConfigureAwait(false);
        var sdk = await ExecuteAsync(["--version"], "sdk").ConfigureAwait(false);
        if (!sdk.Trim().StartsWith("8.0.", StringComparison.Ordinal))
        {
            throw new InvalidDataException("An installed SDK8 is required; no SDK installation was attempted.");
        }

        await ExecuteAsync(["restore", "--configfile", configuration, "-p:ConsumerPackageVersion=" + version, .. ConsumerProperties], "restore").ConfigureAwait(false);
        List<string> build = ["build", "--no-restore", "--configuration", "Release", "-p:ConsumerPackageVersion=" + version, .. ConsumerProperties];
        if (feed is not null)
        {
            build.Add("-p:DefineConstants=CANDIDATE_FLUENT");
        }

        await ExecuteAsync(build, "build").ConfigureAwait(false);
        return Path.Combine(directory, "bin/Release/net8.0");

        async Task<string> ExecuteAsync(IEnumerable<string> arguments, string step)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var info = ProcessRunner.Create("dotnet", directory, arguments);
            info.Environment["NUGET_PACKAGES"] = Path.Combine(scratch, name + "-packages");
            var result = await ProcessRunner.RunAsync(info, null, Path.Combine(output, name + "-" + step + ".log"), echo: true, timeout.Token).ConfigureAwait(false);
            return result.ExitCode == 0 ? result.Output : throw new InvalidOperationException($"Consumer {name}/{step} failed; native diagnostics were retained.");
        }
    }

    private static async Task RunCallerAsync(string directory, string hash, string log, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var result = await ProcessRunner.RunAsync(ProcessRunner.Create("dotnet", directory, [Caller, hash]), null, log, echo: true, timeout.Token).ConfigureAwait(false);
        if (result.ExitCode != 0 || !result.Output.Contains("ABI_OK", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The source or old-binary consumer did not complete on the candidate graph.");
        }
    }
}
