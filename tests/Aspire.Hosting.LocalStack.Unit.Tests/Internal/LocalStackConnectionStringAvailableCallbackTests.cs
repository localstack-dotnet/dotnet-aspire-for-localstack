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
        var localStackResource = Substitute.For<ILocalStackResource>();
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);

        localStackResource.Options.Returns(options);

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
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true, regionName: "us-west-2");
            var localStackAnnotations = new ResourceAnnotationCollection();
            var stackAnnotations = new ResourceAnnotationCollection();
            var connectionString = "http://localhost:4566";
            var localStackResource = Substitute.For<ILocalStackResource>();
            localStackResource.Name.Returns("localstack");
            localStackResource.Options.Returns(options);
            localStackResource.Annotations.Returns(localStackAnnotations);
            localStackResource.ConnectionStringExpression.Returns(ReferenceExpression.Create($"{connectionString}"));

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
            localStackAnnotations.Add(new LocalStackReferenceAnnotation(stackResource));

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
}
