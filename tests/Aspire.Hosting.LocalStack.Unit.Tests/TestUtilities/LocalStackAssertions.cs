namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class LocalStackAssertions
{
    public static async Task ShouldHaveLocalStackEnabledAnnotation(this IResource resource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation!.LocalStackResource).IsSameReferenceAs(expectedLocalStack);
    }

    public static async Task ShouldHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        await Assert.That(annotation).IsNotNull();
    }

    public static async Task ShouldHaveReferenceToResource(this ILocalStackResource localStackResource, IResource expectedTargetResource)
    {
        ArgumentNullException.ThrowIfNull(localStackResource);
        ArgumentNullException.ThrowIfNull(expectedTargetResource);

        var annotation = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .SingleOrDefault(a => ReferenceEquals(a.Resource, expectedTargetResource));

        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation!.Resource).IsSameReferenceAs(expectedTargetResource);
    }

    public static async Task ShouldNotHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        await Assert.That(annotation).IsNull();
    }

    public static async Task ShouldHaveLocalStackEnvironmentConfiguration(this ProjectResource projectResource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(projectResource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);
#pragma warning disable MA0015 // ArgumentNullException.ThrowIfNull should be preferred over null check
        ArgumentNullException.ThrowIfNull(expectedLocalStack.Options);
#pragma warning restore MA0015

        await projectResource.ShouldHaveLocalStackEnabledAnnotation(expectedLocalStack);

        var envAnnotations = projectResource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        await Assert.That(envAnnotations).IsNotEmpty();
        await Assert.That(envAnnotations.Count > 0).IsTrue()
            .Because("Project should have environment callback annotations for LocalStack configuration");
    }

    public static async Task ShouldHaveResourceCount<T>(this DistributedApplication app, int expectedCount)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);

        var actualCount = app.GetResources<T>().Count();
        await Assert.That(actualCount).IsEqualTo(expectedCount);
    }

    public static async Task ShouldWaitFor(this IResource resource, IResource dependencyResource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(dependencyResource);

        var tryGetAnnotationsOfType = resource.TryGetAnnotationsOfType<WaitAnnotation>(out var annotations);
        await Assert.That(tryGetAnnotationsOfType).IsTrue();
        await Assert.That(annotations).IsNotNull();

        var hasWaitForDependencyResource = annotations!.Any(annotation => annotation.Resource == dependencyResource);
        await Assert.That(hasWaitForDependencyResource).IsTrue()
            .Because($"Resource '{resource.Name}' should wait for resource '{dependencyResource.Name}'.");
    }
}
