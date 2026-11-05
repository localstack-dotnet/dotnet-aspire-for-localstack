namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackRuntimeResolverTests
{
    private static readonly LocalStackRuntimeCatalog Catalog = new(
        new LocalStackRuntimeImage("docker.io", "localstack/localstack", "test-current", null),
        new LocalStackRuntimeImage("docker.io", "localstack/localstack", "test-legacy", null));

    [Test]
    public async Task Legacy_Preset_Does_Not_Mutate_Explicit_Image_Fields()
    {
        var options = new LocalStackContainerOptions
        {
            ContainerRegistry = "mirror.example",
            ContainerImage = "team/localstack",
            ContainerImageTag = "test-tag",
        };

        var returned = options.WithLegacy();

        await Assert.That(returned).IsSameReferenceAs(options);
        await Assert.That(options.ContainerRegistry).IsEqualTo("mirror.example");
        await Assert.That(options.ContainerImage).IsEqualTo("team/localstack");
        await Assert.That(options.ContainerImageTag).IsEqualTo("test-tag");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Explicit_Fields_Win_In_Either_Call_Order(bool presetFirst)
    {
        var options = new LocalStackContainerOptions();
        if (presetFirst)
        {
            options.WithLegacy();
        }

        options.ContainerRegistry = "mirror.example";
        if (!presetFirst)
        {
            options.WithLegacy();
        }

        var selected = LocalStackRuntimeResolver.Resolve(options, Catalog);

        await Assert.That(selected.Image.Registry).IsEqualTo("mirror.example");
        await Assert.That(selected.Image.Image).IsEqualTo("localstack/localstack");
        await Assert.That(selected.Image.Tag).IsEqualTo("test-legacy");
        await Assert.That(selected.Kind).IsEqualTo(LocalStackRuntimeKind.Custom);
        await Assert.That(options.ContainerImageTag).IsNull();
    }

    [Test]
    public async Task Explicit_Tag_Clears_Preset_Digest()
    {
        var catalog = Catalog with
        {
            Legacy = new LocalStackRuntimeImage("docker.io", "localstack/localstack", null, new string('a', 64)),
        };
        var options = new LocalStackContainerOptions
        {
            ContainerImageTag = "custom-tag",
        };
        options.WithLegacy();

        var selected = LocalStackRuntimeResolver.Resolve(options, catalog);

        await Assert.That(selected.Image.Tag).IsEqualTo("custom-tag");
        await Assert.That(selected.Image.Digest).IsNull();
        await Assert.That(selected.Kind).IsEqualTo(LocalStackRuntimeKind.Custom);
    }

    [Test]
    public async Task Annotation_Identity_Preserves_Core_Digest_And_Default_Registry()
    {
        var annotation = new ContainerImageAnnotation
        {
            Image = "localstack/localstack",
            SHA256 = new string('b', 64),
        };

        var image = LocalStackRuntimeResolver.FromAnnotation(annotation);

        await Assert.That(image.Registry).IsEqualTo("docker.io");
        await Assert.That(image.Image).IsEqualTo("localstack/localstack");
        await Assert.That(image.Tag).IsNull();
        await Assert.That(image.Digest).IsEqualTo(new string('b', 64));
    }

    [Test]
    public async Task Legacy_False_Restores_Current_Preference()
    {
        var options = new LocalStackContainerOptions();
        options.WithLegacy().WithLegacy(false);

        var selected = LocalStackRuntimeResolver.Resolve(options, Catalog);

        await Assert.That(selected.Image.Tag).IsEqualTo("test-current");
        await Assert.That(selected.Kind).IsEqualTo(LocalStackRuntimeKind.KnownCurrent);
    }

    [Test]
    [Arguments(null, null, null, "docker.io", "localstack/localstack", "test-legacy", "KnownLegacy")]
    [Arguments("mirror.example", null, null, "mirror.example", "localstack/localstack", "test-legacy", "Custom")]
    [Arguments(null, "team/localstack", null, "docker.io", "team/localstack", "test-legacy", "Custom")]
    [Arguments(null, null, "custom-tag", "docker.io", "localstack/localstack", "custom-tag", "Custom")]
    [Arguments("mirror.example", "team/localstack", "custom-tag", "mirror.example", "team/localstack", "custom-tag", "Custom")]
    [Arguments(null, null, "", "docker.io", "localstack/localstack", "", "Custom")]
    public async Task Explicit_Components_Override_Only_Their_Preset_Component(
        string? registry, string? image, string? tag,
        string expectedRegistry, string expectedImage, string expectedTag, string expectedKind)
    {
        var options = new LocalStackContainerOptions
        {
            ContainerRegistry = registry,
            ContainerImage = image,
            ContainerImageTag = tag,
        };
        options.WithLegacy();

        var selected = LocalStackRuntimeResolver.Resolve(options, Catalog);

        await Assert.That(selected.Image.Registry).IsEqualTo(expectedRegistry);
        await Assert.That(selected.Image.Image).IsEqualTo(expectedImage);
        await Assert.That(selected.Image.Tag).IsEqualTo(expectedTag);
        await Assert.That(selected.Kind.ToString()).IsEqualTo(expectedKind);
    }

    [Test]
    [Arguments("mirror.example", "test-legacy", null)]
    [Arguments("Docker.io", "test-legacy", null)]
    [Arguments("index.docker.io", "test-legacy", null)]
    [Arguments("docker.io", "latest", null)]
    [Arguments("docker.io", null, "different-digest")]
    public async Task Different_Mirror_Or_Digest_Is_Custom(string registry, string? tag, string? digest)
    {
        var image = new LocalStackRuntimeImage(registry, "localstack/localstack", tag, digest);

        var kind = LocalStackRuntimeResolver.Classify(image, Catalog);

        await Assert.That(kind).IsEqualTo(LocalStackRuntimeKind.Custom);
    }

    [Test]
    [Arguments("", "KnownCurrent")]
    [Arguments("sha256:", "Custom")]
    public async Task Qualified_Digest_Uses_Core_Bare_Hex_Identity(string prefix, string expectedKind)
    {
        var digest = new string('a', 64);
        var catalog = Catalog with
        {
            Current = new LocalStackRuntimeImage("docker.io", "localstack/localstack", null, digest),
        };
        var annotation = new ContainerImageAnnotation
        {
            Image = "localstack/localstack",
            SHA256 = prefix + digest,
        };

        var kind = LocalStackRuntimeResolver.Classify(LocalStackRuntimeResolver.FromAnnotation(annotation), catalog);

        await Assert.That(kind.ToString()).IsEqualTo(expectedKind);
    }

    [Test]
    public async Task Default_Container_Uses_Qualified_Current_Image()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var resource = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
        await Assert.That(resource).IsNotNull();
        var image = resource!.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();

        await Assert.That(image.Registry).IsEqualTo("docker.io");
        await Assert.That(image.Image).IsEqualTo("localstack/localstack");
        await Assert.That(image.Tag).IsEqualTo("2026.10.0");
        await Assert.That(image.SHA256).IsNull();
    }

    [Test]
    public async Task Production_Catalog_Resolves_Qualified_Legacy_Image()
    {
        var options = new LocalStackContainerOptions();
        options.WithLegacy();

        var selected = LocalStackRuntimeResolver.Resolve(options, LocalStackRuntimeCatalog.Production);

        await Assert.That(selected.Image.Registry).IsEqualTo("docker.io");
        await Assert.That(selected.Image.Image).IsEqualTo("localstack/localstack");
        await Assert.That(selected.Image.Tag).IsEqualTo("4.14.0");
        await Assert.That(selected.Image.Digest).IsNull();
        await Assert.That(selected.Kind).IsEqualTo(LocalStackRuntimeKind.KnownLegacy);
    }

    [Test]
    [Arguments(null, "docker.io", "KnownCurrent")]
    [Arguments("docker.io", "docker.io", "KnownCurrent")]
    [Arguments("", "", "Custom")]
    [Arguments("index.docker.io", "index.docker.io", "Custom")]
    public async Task Annotation_Registry_Normalizes_Only_Absent_Default(
        string? registry, string expectedRegistry, string expectedKind)
    {
        var annotation = new ContainerImageAnnotation
        {
            Registry = registry,
            Image = "localstack/localstack",
            Tag = "test-current",
        };

        var image = LocalStackRuntimeResolver.FromAnnotation(annotation);

        await Assert.That(image.Registry).IsEqualTo(expectedRegistry);
        await Assert.That(LocalStackRuntimeResolver.Classify(image, Catalog).ToString()).IsEqualTo(expectedKind);
    }

    [Test]
    public async Task Legacy_Preset_Rejects_Null_Options()
    {
        await Assert.That(() => LocalStackContainerOptionsExtensions.WithLegacy(null!))
            .ThrowsExactly<ArgumentNullException>();
    }
}
