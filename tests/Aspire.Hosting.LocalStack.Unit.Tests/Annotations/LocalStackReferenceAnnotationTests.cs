namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackReferenceAnnotationTests
{
    [Test]
    public async Task LocalStackReferenceAnnotation_Should_Implement_IResourceAnnotation()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("test-resource");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        await Assert.That(annotation).IsAssignableTo<IResourceAnnotation>();
    }

    [Test]
    public async Task LocalStackReferenceAnnotation_Should_Store_Target_Resource_Reference()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("test-resource");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        await Assert.That(annotation.Resource).IsSameReferenceAs(testResource);
    }

    [Test]
    public async Task LocalStackReferenceAnnotation_Should_Throw_ArgumentNullException_For_Null_Target_Resource()
    {
        await Assert.That(() => new LocalStackReferenceAnnotation(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task LocalStackReferenceAnnotation_Should_Maintain_Same_Target_Resource_Reference()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns("valid-resource-name");

        var annotation = new LocalStackReferenceAnnotation(testResource);

        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation.Resource).IsSameReferenceAs(testResource);

        // Multiple calls should return the same reference
        await Assert.That(annotation.Resource).IsSameReferenceAs(annotation.Resource);
    }

    [Test]
    public async Task LocalStackReferenceAnnotation_Should_Accept_Resource_With_Empty_Name()
    {
        var testResource = Substitute.For<IResource>();
        testResource.Name.Returns(string.Empty);

        var annotation = new LocalStackReferenceAnnotation(testResource);

        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation.Resource).IsSameReferenceAs(testResource);
        await Assert.That(annotation.Resource.Name).IsEqualTo(string.Empty);
    }
}
