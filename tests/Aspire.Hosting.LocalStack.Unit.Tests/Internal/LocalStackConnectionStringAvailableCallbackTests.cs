using Amazon;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

[NotInParallel("AwsSdkPipelineCustomizer")]
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
        var localStackResource = new TestLocalStackResource(
            "localstack",
            TestDataBuilders.CreateHostingState(enabled: false),
            "http://localhost:4566");

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        await callback(localStackResource, null!, CancellationToken.None);

        builder.DidNotReceive().CreateResourceBuilder(Arg.Any<IResource>());
    }

    [Test]
    public async Task Callback_Should_Assign_Profileless_Region_Pinned_AWSSDKConfig_For_LocalStack_CDK_Stack()
    {
        var previousCredentialGenerators = AWSConfigs.AWSCredentialsGenerators;
        LocalStackCdkAssetUploadEndpointCustomizer.Deregister();

        try
        {
            var builder = DistributedApplication.CreateBuilder([]);
            var stackAnnotations = new ResourceAnnotationCollection();
            var connectionString = "http://localhost:4566";
            var localStackResource = new TestLocalStackResource(
                "localstack",
                TestDataBuilders.CreateHostingState(regionName: "us-west-2"),
                connectionString);

            var awsSdkConfig = Substitute.For<IAWSSDKConfig>();
            awsSdkConfig.Profile = "default";
            awsSdkConfig.Region = RegionEndpoint.USEast1;
            awsSdkConfig.SDKValidationEnabled = true;

            var stackResource = Substitute.For<IStackResource>();
            stackResource.Name.Returns("stack");
            stackResource.Annotations.Returns(stackAnnotations);
            IAWSSDKConfig? assignedConfig = null;
            stackResource.AWSSDKConfig.Returns(_ => assignedConfig ?? awsSdkConfig);
            stackResource
                .When(static resource => resource.AWSSDKConfig = Arg.Any<IAWSSDKConfig?>())
                .Do(callInfo => assignedConfig = callInfo.Arg<IAWSSDKConfig?>());
            stackAnnotations.Add(new LocalStackEnabledAnnotation(localStackResource));
            localStackResource.Annotations.Add(new LocalStackReferenceAnnotation(stackResource));

            var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

            await callback(localStackResource, null!, CancellationToken.None);

            await Assert.That(assignedConfig).IsNotNull();
            await Assert.That(ReferenceEquals(assignedConfig, awsSdkConfig)).IsFalse();
            await Assert.That(assignedConfig!.Profile).IsNull();
            await Assert.That(assignedConfig.Region).IsEqualTo(RegionEndpoint.USWest2);
            await Assert.That(assignedConfig.SDKValidationEnabled).IsTrue();
        }
        finally
        {
            LocalStackCdkAssetUploadEndpointCustomizer.Deregister();
            AWSConfigs.AWSCredentialsGenerators = previousCredentialGenerators;
        }
    }

    [Test]
    public async Task Callback_Should_Configure_DynamoDb_Streams_Event_Source_Resource()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var helperResource = TestResourceFactory.CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper");
        var connectionString = "http://localhost:4566";

        var localStackResource = new TestLocalStackResource(
            "localstack",
            TestDataBuilders.CreateHostingState(regionName: "eu-central-1"),
            connectionString);
        helperResource.Annotations.Add(new LocalStackEnabledAnnotation(localStackResource));

        localStackResource.Annotations.Add(new LocalStackReferenceAnnotation(helperResource));

        var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

        await callback(localStackResource, null!, CancellationToken.None);

        var envAnnotation = helperResource.Annotations.OfType<EnvironmentCallbackAnnotation>().Single();
        var env = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), helperResource, env);
        await envAnnotation.Callback(context);

        await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB"]).IsEqualTo("http://localhost:4566/");
        await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"]).IsEqualTo("http://localhost:4566/");
        await Assert.That(env["AWS_DEFAULT_REGION"]).IsEqualTo("eu-central-1");
    }

    private sealed class TestLocalStackResource(
        string name,
        LocalStackHostingState hostingState,
        string connectionString) : ILocalStackResource, ILocalStackHostingStateProvider
    {
        public string Name { get; } = name;

        public ResourceAnnotationCollection Annotations { get; } = [];

        public ReferenceExpression ConnectionStringExpression { get; } = ReferenceExpression.Create($"{connectionString}");

        public LocalStackHostingState HostingState { get; } = hostingState;

#pragma warning disable CS0618
        public ILocalStackOptions Options => throw new NotSupportedException();
#pragma warning restore CS0618
    }
}
