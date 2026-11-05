namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

internal enum RuntimeTestProfile
{
    Current,
    Legacy,
}

internal sealed record RuntimeTestImage(string Registry, string Image, string? Tag, string? Digest);

internal sealed class RuntimeTestConfiguration
{
    private static readonly Task<RuntimeTestConfiguration> ProcessConfiguration = ReadAsync(
        Environment.GetEnvironmentVariable("LOCALSTACK_TEST_RUNTIME"),
        Path.Combine(AppContext.BaseDirectory, "runtime-images.json"),
        Environment.GetEnvironmentVariable("LOCALSTACK_TEST_RESULTS_DIRECTORY"));

    private RuntimeTestConfiguration(RuntimeTestProfile profile, RuntimeTestImage expectedImage, string outputDirectory)
    {
        Profile = profile;
        ExpectedImage = expectedImage;
        OutputDirectory = outputDirectory;
    }

    internal RuntimeTestProfile Profile { get; }

    internal RuntimeTestImage ExpectedImage { get; }

    internal string OutputDirectory { get; }

    internal static Task<RuntimeTestConfiguration> ForCurrentProcessAsync(CancellationToken cancellationToken = default)
        => ProcessConfiguration.WaitAsync(cancellationToken);

    internal LocalStackContainerOptions ConfigureContainer(LocalStackContainerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (Profile == RuntimeTestProfile.Legacy)
        {
            options.WithLegacy();
        }

        return options;
    }

    internal string[] CreateAppHostArguments() =>
    [
        "Aspire:Hosting:LocalStack:Enabled=true",
        Profile == RuntimeTestProfile.Legacy
            ? "Playground:LocalStack:UseLegacy=true"
            : "Playground:LocalStack:UseLegacy=false",
    ];

    internal static RuntimeTestImage ReadEffectiveImage(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var annotation = resource.Annotations.OfType<ContainerImageAnnotation>().LastOrDefault()
            ?? throw new InvalidOperationException("The selected resource has no container image annotation.");
        return new RuntimeTestImage(annotation.Registry ?? "docker.io", annotation.Image, annotation.Tag, annotation.SHA256);
    }

    internal async Task<string> WriteImageReceiptAsync(
        IResource resource, string scenarioGroup, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var image = ReadEffectiveImage(resource);
        if (image != ExpectedImage)
        {
            throw new InvalidOperationException("Selected runtime image differs from the independent qualification record.");
        }

        Directory.CreateDirectory(outputDirectory);
        var id = Profile == RuntimeTestProfile.Legacy ? "legacy" : "current";
        var path = Path.Combine(outputDirectory, $"{id}-image-{Guid.NewGuid():N}.json");
        var receipt = JsonSerializer.Serialize(new
        {
            schema_version = 1,
            runtime_id = id,
            scenario_group = scenarioGroup,
            image = new { registry = image.Registry, image = image.Image, tag = image.Tag, digest = image.Digest },
        });
        await File.WriteAllTextAsync(path, receipt, cancellationToken);
        return path;
    }

    internal static async Task<RuntimeTestConfiguration> ReadAsync(
        string? selector, string qualificationFile, string? resultsDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var profile = selector switch
        {
            null or "current" => RuntimeTestProfile.Current,
            "legacy" => RuntimeTestProfile.Legacy,
            _ => throw new ArgumentException("Runtime selector must be current or legacy.", nameof(selector)),
        };
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(qualificationFile, cancellationToken));
        if (document.RootElement.GetProperty("schema_version").GetInt32() != 1)
        {
            throw new InvalidOperationException("Unsupported runtime qualification schema.");
        }

        var id = profile == RuntimeTestProfile.Legacy ? "legacy" : "current";
        var row = document.RootElement.GetProperty("images").GetProperty(id);
        var image = new RuntimeTestImage(
            RequiredString(row, "registry"),
            RequiredString(row, "image"),
            row.GetProperty("tag").GetString(),
            row.GetProperty("digest").GetString());
        if ((image.Tag is null) == (image.Digest is null))
        {
            throw new InvalidOperationException("A qualified runtime needs exactly one tag or digest.");
        }

        var output = resultsDirectory ?? Path.Combine(AppContext.BaseDirectory, "runtime-results", id);
        return new RuntimeTestConfiguration(profile, image, Path.GetFullPath(output));
    }

    private static string RequiredString(JsonElement row, string field)
    {
        var value = row.GetProperty(field).GetString();
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException("Runtime qualification has an empty required image field.");
    }
}
