namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackConnectionStringAvailableCallbackTests
{
    [Fact]
    public void CreateCallback_Should_Return_Valid_Callback()
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        Assert.NotNull(callback);
    }

    [Fact]
    public void CreateCallback_Should_Throw_ArgumentNullException_For_Null_Builder()
    {
        Assert.Throws<ArgumentNullException>(() => LocalStackConnectionStringAvailableCallback.CreateCallback(null!));
    }

    [Fact]
    public void CreateCallback_Should_Return_Function_With_Correct_Signature()
    {
        var builder = Substitute.For<IDistributedApplicationBuilder>();

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        Assert.IsType<Func<ILocalStackResource, ConnectionStringAvailableEvent, CancellationToken, Task>>(callback);
    }

    [Fact]
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
