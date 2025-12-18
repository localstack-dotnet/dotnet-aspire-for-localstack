namespace Aspire.Hosting.LocalStack.Unit.Tests.Annotations;

public class LocalStackEnabledAnnotationTests
{
    [Test]
    public async Task LocalStackEnabledAnnotation_Should_Implement_IResourceAnnotation()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        await Assert.That(annotation).IsAssignableTo<IResourceAnnotation>();
    }

    [Test]
    public async Task LocalStackEnabledAnnotation_Should_Store_LocalStack_Resource_Reference()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        await Assert.That(annotation.LocalStackResource).IsSameReferenceAs(localStackResource);
    }

    [Test]
    public async Task LocalStackEnabledAnnotation_Should_Throw_ArgumentNullException_For_Null_LocalStack_Resource()
    {
        await Assert.That(() => new LocalStackEnabledAnnotation(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task LocalStackEnabledAnnotation_Should_Maintain_Same_Resource_Reference()
    {
        var localStackResource = CreateTestLocalStackResource();

        var annotation = new LocalStackEnabledAnnotation(localStackResource);

        // Test that the resource reference remains consistent
        await Assert.That(annotation).IsNotNull();
        await Assert.That(annotation.LocalStackResource).IsNotNull();
        await Assert.That(annotation.LocalStackResource).IsSameReferenceAs(localStackResource);

        // Multiple calls should return the same reference
        await Assert.That(annotation.LocalStackResource).IsSameReferenceAs(annotation.LocalStackResource);
    }

    private static LocalStackResource CreateTestLocalStackResource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
        return new LocalStackResource("test-localstack", options);
    }
}
