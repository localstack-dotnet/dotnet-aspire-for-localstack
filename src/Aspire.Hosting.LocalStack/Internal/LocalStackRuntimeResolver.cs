using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack.Container;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackRuntimeResolver
{
    internal static LocalStackRuntimeImage FromAnnotation(ContainerImageAnnotation image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new LocalStackRuntimeImage(
            image.Registry ?? LocalStackContainerImageTags.Registry,
            image.Image,
            image.Tag,
            image.SHA256);
    }

    internal static LocalStackRuntimeSelection Resolve(LocalStackContainerOptions options, LocalStackRuntimeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(catalog);

        var preset = options.RuntimePreset == LocalStackRuntimePreset.Legacy ? catalog.Legacy : catalog.Current;
        var image = new LocalStackRuntimeImage(
            options.ContainerRegistry ?? preset.Registry,
            options.ContainerImage ?? preset.Image,
            options.ContainerImageTag ?? preset.Tag,
            options.ContainerImageTag is null ? preset.Digest : null);

        return new LocalStackRuntimeSelection(image, Classify(image, catalog));
    }

    internal static LocalStackRuntimeKind Classify(LocalStackRuntimeImage image, LocalStackRuntimeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(catalog);

        if (image == catalog.Current)
        {
            return LocalStackRuntimeKind.KnownCurrent;
        }

        return image == catalog.Legacy ? LocalStackRuntimeKind.KnownLegacy : LocalStackRuntimeKind.Custom;
    }
}
