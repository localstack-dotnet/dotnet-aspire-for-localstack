namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class LocalStackAssertions
{
    public static void ShouldHaveLocalStackEnabledAnnotation(this IResource resource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
        Assert.Same(expectedLocalStack, annotation.LocalStackResource);
    }

    public static void ShouldHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
    }

    public static void ShouldHaveReferenceToResource(this ILocalStackResource localStackResource, IResource expectedTargetResource)
    {
        ArgumentNullException.ThrowIfNull(localStackResource);
        ArgumentNullException.ThrowIfNull(expectedTargetResource);

        var annotation = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .SingleOrDefault(a => ReferenceEquals(a.Resource, expectedTargetResource));

        Assert.NotNull(annotation);
        Assert.Same(expectedTargetResource, annotation.Resource);
    }

    public static void ShouldNotHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.Null(annotation);
    }

    public static void ShouldHaveLocalStackEnvironmentConfiguration(this ProjectResource projectResource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(projectResource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);
#pragma warning disable MA0015
        ArgumentNullException.ThrowIfNull(expectedLocalStack.Options);
#pragma warning restore MA0015

        projectResource.ShouldHaveLocalStackEnabledAnnotation(expectedLocalStack);

        var envAnnotations = projectResource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        Assert.NotEmpty(envAnnotations);
        Assert.True(envAnnotations.Count > 0, "Project should have environment callback annotations for LocalStack configuration");
    }

    public static void ShouldHaveResourceCount<T>(this DistributedApplication app, int expectedCount)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);

        var actualCount = app.GetResources<T>().Count();
        Assert.Equal(expectedCount, actualCount);
    }

    public static void ShouldWaitFor(this IResource resource, IResource dependencyResource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(dependencyResource);

        var tryGetAnnotationsOfType = resource.TryGetAnnotationsOfType<WaitAnnotation>(out var annotations);
        Assert.True(tryGetAnnotationsOfType);
        Assert.NotNull(annotations);

        var hasWaitForDependencyResource = annotations.Any(annotation => annotation.Resource == dependencyResource);
        Assert.True(hasWaitForDependencyResource, $"Resource '{resource.Name}' should wait for resource '{dependencyResource.Name}'.");
    }
}
