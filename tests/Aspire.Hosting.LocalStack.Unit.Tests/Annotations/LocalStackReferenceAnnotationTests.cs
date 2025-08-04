namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackReferenceAnnotationTests
{
    [Fact]
    public void LocalStackReferenceAnnotation_Should_Implement_IResourceAnnotation()
    {
        const string targetResource = "test-resource";

        var annotation = new LocalStackReferenceAnnotation(targetResource);

        Assert.IsType<IResourceAnnotation>(annotation, exactMatch: false);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Store_Target_Resource_Reference()
    {
        const string targetResource = "test-resource";

        var annotation = new LocalStackReferenceAnnotation(targetResource);

        Assert.Equal(targetResource, annotation.TargetResource);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Throw_ArgumentNullException_For_Null_Target_Resource()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStackReferenceAnnotation(null!));
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Maintain_Same_Target_Resource_Name()
    {
        const string targetResource = "valid-resource-name";

        var annotation = new LocalStackReferenceAnnotation(targetResource);

        Assert.NotNull(annotation);
        Assert.Equal(targetResource, annotation.TargetResource);

        // Multiple calls should return the same value
        Assert.Equal(annotation.TargetResource, annotation.TargetResource);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LocalStackReferenceAnnotation_Should_Accept_Empty_Or_Whitespace_Target_Resource_Names(string name)
    {
        // The implementation actually accepts empty/whitespace strings
        var annotation = new LocalStackReferenceAnnotation(name);

        Assert.NotNull(annotation);
        Assert.Equal(name, annotation.TargetResource);
    }
}
