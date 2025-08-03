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
    public void LocalStackReferenceAnnotation_Should_Accept_Valid_Target_Resource_Name()
    {
        const string targetResource = "valid-resource-name";

        var annotation = new LocalStackReferenceAnnotation(targetResource);

        Assert.NotNull(annotation);
        Assert.Equal(targetResource, annotation.TargetResource);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Have_Immutable_Target_Resource_Property()
    {
        const string targetResource = "test-resource";
        var annotation = new LocalStackReferenceAnnotation(targetResource);

        var retrievedTarget = annotation.TargetResource;

        Assert.Equal(targetResource, retrievedTarget);
        Assert.False(typeof(LocalStackReferenceAnnotation).GetProperty(nameof(LocalStackReferenceAnnotation.TargetResource))?.CanWrite);
    }
}
