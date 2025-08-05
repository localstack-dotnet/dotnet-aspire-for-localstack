namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackReferenceAnnotationTests
{
    [Fact]
    public void LocalStackReferenceAnnotation_Should_Implement_IResourceAnnotation()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("test-resource");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        Assert.IsType<IResourceAnnotation>(annotation, exactMatch: false);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Store_Target_Resource_Reference()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("test-resource");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        Assert.Same(testResource, annotation.Resource);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Throw_ArgumentNullException_For_Null_Target_Resource()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStackReferenceAnnotation(null!));
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Maintain_Same_Target_Resource_Reference()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("valid-resource-name");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        Assert.NotNull(annotation);
        Assert.Same(testResource, annotation.Resource);

        // Multiple calls should return the same reference
        Assert.Same(annotation.Resource, annotation.Resource);
    }

    [Fact]
    public void LocalStackReferenceAnnotation_Should_Accept_Resource_With_Empty_Name()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns(string.Empty);

        var annotation = new LocalStackReferenceAnnotation(testResource);

        Assert.NotNull(annotation);
        Assert.Same(testResource, annotation.Resource);
        Assert.Equal(string.Empty, annotation.Resource.Name);
    }
}
