using Aspire.Hosting.LocalStack.Container;

namespace Aspire.Hosting.LocalStack.Internal;

internal enum LocalStackRuntimePreset
{
    Current,
    Legacy,
}

internal enum LocalStackRuntimeKind
{
    KnownCurrent,
    KnownLegacy,
    Custom,
}

internal sealed record LocalStackRuntimeImage(string Registry, string Image, string? Tag, string? Digest);

internal sealed record LocalStackRuntimeCatalog(LocalStackRuntimeImage Current, LocalStackRuntimeImage Legacy)
{
    internal static LocalStackRuntimeCatalog Production { get; } = new(
        new LocalStackRuntimeImage(LocalStackContainerImageTags.Registry, LocalStackContainerImageTags.Image, LocalStackContainerImageTags.Tag, null),
        new LocalStackRuntimeImage(LocalStackContainerImageTags.Registry, LocalStackContainerImageTags.Image, LocalStackContainerImageTags.LegacyTag, null));
}

internal sealed record LocalStackRuntimeSelection(LocalStackRuntimeImage Image, LocalStackRuntimeKind Kind);
