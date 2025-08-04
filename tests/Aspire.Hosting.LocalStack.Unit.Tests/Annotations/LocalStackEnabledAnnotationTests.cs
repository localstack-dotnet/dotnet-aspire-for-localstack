namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackEnabledAnnotationTests
{
    [Fact]
    public void LocalStackEnabledAnnotation_Should_Implement_IResourceAnnotation()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        Assert.IsType<IResourceAnnotation>(annotation, exactMatch: false);
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Store_LocalStack_Resource_Reference()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        Assert.Same(localStackResource, annotation.LocalStackResource);
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Throw_ArgumentNullException_For_Null_LocalStack_Resource()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStackEnabledAnnotation(null!));
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Maintain_Same_Resource_Reference()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        // Test that the resource reference remains consistent
        Assert.NotNull(annotation);
        Assert.NotNull(annotation.LocalStackResource);
        Assert.Same(localStackResource, annotation.LocalStackResource);

        // Multiple calls should return the same reference
        Assert.Same(annotation.LocalStackResource, annotation.LocalStackResource);
    }

    private static LocalStackResource CreateTestLocalStackResource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
        return new LocalStackResource("test-localstack", options);
    }
}
