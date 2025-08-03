namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackEnabledAnnotationTests
{
    [Fact]
    public void LocalStackEnabledAnnotation_Should_Implement_IResourceAnnotation()
    {
        var mockLocalStackResource = Substitute.For<ILocalStackResource>();

        var annotation = new LocalStackEnabledAnnotation(mockLocalStackResource);

        Assert.IsType<IResourceAnnotation>(annotation, exactMatch: false);
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Store_LocalStack_Resource_Reference()
    {
        var mockLocalStackResource = Substitute.For<ILocalStackResource>();

        var annotation = new LocalStackEnabledAnnotation(mockLocalStackResource);

        Assert.Equal(mockLocalStackResource, annotation.LocalStackResource);
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Throw_ArgumentNullException_For_Null_LocalStack_Resource()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStackEnabledAnnotation(null!));
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Accept_Valid_LocalStack_Resource()
    {
        var mockLocalStackResource = Substitute.For<ILocalStackResource>();

        var annotation = new LocalStackEnabledAnnotation(mockLocalStackResource);

        Assert.NotNull(annotation);
        Assert.NotNull(annotation.LocalStackResource);
        Assert.Same(mockLocalStackResource, annotation.LocalStackResource);
    }

    [Fact]
    public void LocalStackEnabledAnnotation_Should_Have_Immutable_LocalStack_Resource_Property()
    {
        var mockLocalStackResource = Substitute.For<ILocalStackResource>();
        var annotation = new LocalStackEnabledAnnotation(mockLocalStackResource);

        var retrievedResource = annotation.LocalStackResource;

        Assert.Same(mockLocalStackResource, retrievedResource);
        Assert.False(typeof(LocalStackEnabledAnnotation).GetProperty(nameof(LocalStackEnabledAnnotation.LocalStackResource))?.CanWrite);
    }
}
