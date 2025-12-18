namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackConnectionStringAvailableCallbackTests
{
    [Test]
    public async Task CreateCallback_Should_Return_Valid_Callback()
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        await Assert.That(callback).IsNotNull();
    }

    [Test]
    public async Task CreateCallback_Should_Throw_ArgumentNullException_For_Null_Builder()
    {
        await Assert.That(() => LocalStackConnectionStringAvailableCallback.CreateCallback(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task CreateCallback_Should_Return_Function_With_Correct_Signature()
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        await Assert.That(callback).IsTypeOf<Func<ILocalStackResource, ConnectionStringAvailableEvent, CancellationToken, Task>>();
    }

    [Test]
    public async Task Callback_Should_Skip_When_UseLocalStack_Is_False()
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();
        var localStackResource = Substitute.For<ILocalStackResource>();
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);

        localStackResource.Options.Returns(options);

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        await callback(localStackResource, null!, CancellationToken.None);

        builder.DidNotReceive().CreateResourceBuilder(Arg.Any<IResource>());
    }
}
