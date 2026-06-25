using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Internal;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

[NotInParallel("AwsSdkPipelineCustomizer")]
public class LocalStackCdkAssetUploadEndpointCustomizerTests
{
    [Test]
    public async Task ForcePathStyleHandler_Should_Enable_ForcePathStyle_For_S3_Config()
    {
        var requestContext = Substitute.For<IRequestContext>();
        requestContext.ClientConfig.Returns(new AmazonS3Config());

        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.RequestContext.Returns(requestContext);

        var innerHandler = Substitute.For<IPipelineHandler>();
        var handler = new LocalStackCdkAssetUploadForcePathStyleHandler { InnerHandler = innerHandler };

        handler.InvokeSync(executionContext);

        await Assert.That(((AmazonS3Config)requestContext.ClientConfig).ForcePathStyle).IsTrue();
        innerHandler.Received(1).InvokeSync(executionContext);
    }

    [Test]
    public async Task EndpointRedirectHandler_Should_Swap_Authority_And_Preserve_Path()
    {
        var request = Substitute.For<IRequest>();
        request.Endpoint = new Uri("https://s3.us-east-1.amazonaws.com/my-bucket");

        var requestContext = Substitute.For<IRequestContext>();
        requestContext.Request.Returns(request);

        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.RequestContext.Returns(requestContext);

        var innerHandler = Substitute.For<IPipelineHandler>();
        var handler = new LocalStackCdkAssetUploadEndpointRedirectHandler(new Uri("http://localhost:4566"))
        {
            InnerHandler = innerHandler
        };

        handler.InvokeSync(executionContext);

        await Assert.That(request.Endpoint).IsEqualTo(new Uri("http://localhost:4566/my-bucket"));
        innerHandler.Received(1).InvokeSync(executionContext);
    }

    [Test]
    public async Task Register_Should_Insert_S3_Handlers_Around_S3_Endpoint_Resolver()
    {
        LocalStackCdkAssetUploadEndpointCustomizer.Deregister();
        try
        {
            LocalStackCdkAssetUploadEndpointCustomizer.Register(new Uri("http://localhost:4566"));

            using var client = new AmazonS3Client(
                new BasicAWSCredentials("test", "test"),
                new AmazonS3Config { ServiceURL = "http://localhost:4566", ForcePathStyle = true });

            var handlers = GetRuntimePipeline(client).EnumerateHandlers().ToList();
            var resolverIndex = handlers.FindIndex(static h => h is AmazonS3EndpointResolver);
            var forcePathStyleIndex = handlers.FindIndex(static h => h is LocalStackCdkAssetUploadForcePathStyleHandler);
            var redirectIndex = handlers.FindIndex(static h => h is LocalStackCdkAssetUploadEndpointRedirectHandler);

            await Assert.That(resolverIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(forcePathStyleIndex).IsEqualTo(resolverIndex - 1);
            await Assert.That(redirectIndex).IsEqualTo(resolverIndex + 1);
        }
        finally
        {
            LocalStackCdkAssetUploadEndpointCustomizer.Deregister();
        }
    }

    [Test]
    public async Task Register_Should_Insert_Redirect_Handler_After_Sts_Endpoint_Resolver()
    {
        LocalStackCdkAssetUploadEndpointCustomizer.Deregister();
        try
        {
            LocalStackCdkAssetUploadEndpointCustomizer.Register(new Uri("http://localhost:4566"));

            using var client = new Amazon.SecurityToken.AmazonSecurityTokenServiceClient(
                new BasicAWSCredentials("test", "test"),
                new Amazon.SecurityToken.AmazonSecurityTokenServiceConfig { ServiceURL = "http://localhost:4566" });

            var handlers = GetRuntimePipeline(client).EnumerateHandlers().ToList();
            var resolverIndex = handlers.FindIndex(static h => h is Amazon.SecurityToken.Internal.AmazonSecurityTokenServiceEndpointResolver);
            var redirectIndex = handlers.FindIndex(static h => h is LocalStackCdkAssetUploadEndpointRedirectHandler);

            await Assert.That(resolverIndex).IsGreaterThanOrEqualTo(0);
            await Assert.That(redirectIndex).IsEqualTo(resolverIndex + 1);
        }
        finally
        {
            LocalStackCdkAssetUploadEndpointCustomizer.Deregister();
        }
    }

    private static RuntimePipeline GetRuntimePipeline(AmazonServiceClient client)
    {
        var property = typeof(AmazonServiceClient).GetProperty(
            "RuntimePipeline", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AmazonServiceClient.RuntimePipeline property not found.");

        return property.GetValue(client) as RuntimePipeline
            ?? throw new InvalidOperationException("AmazonServiceClient.RuntimePipeline value is null.");
    }
}
