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
            var builder = TestApplicationBuilder.CreateBuilder([]);
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
        var builder = TestApplicationBuilder.CreateBuilder([]);
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

    [Test]
    public async Task Bare_Explicit_Reference_Emits_Client9()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var localStack = new TestLocalStackResource("localstack",
            TestDataBuilders.CreateHostingState(regionName: "eu-central-1"), "http://localhost:12345");
        var worker = builder.AddResource(new ExecutableResource("worker", "not-run", "."))
            .WithReference(builder.CreateResourceBuilder(localStack));

        await LocalStackConnectionStringAvailableCallback.CreateCallback(builder)(localStack, null!, CancellationToken.None);
        var environment = await EvaluateEnvironment(builder, worker.Resource);
        var expected = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["LocalStack__UseLocalStack"] = "True",
            ["LocalStack__Session__AwsAccessKeyId"] = "test-key",
            ["LocalStack__Session__AwsAccessKey"] = "test-secret",
            ["LocalStack__Session__AwsSessionToken"] = "test-token",
            ["LocalStack__Session__RegionName"] = "eu-central-1",
            ["LocalStack__Config__LocalStackHost"] = "localhost",
            ["LocalStack__Config__UseSsl"] = "False",
            ["LocalStack__Config__UseLegacyPorts"] = "False",
            ["LocalStack__Config__EdgePort"] = "12345",
        };

        await Assert.That(environment.Keys.Count(key => key.StartsWith("LocalStack__", StringComparison.Ordinal))).IsEqualTo(9);
        foreach (var (key, value) in expected)
        {
            await Assert.That(environment[key]).IsEqualTo(value);
        }
    }

    [Test]
    public async Task Explicit_Imported_Stack_Receives_Selected_CF_Client()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var localStack = new TestLocalStackResource("localstack",
            TestDataBuilders.CreateHostingState(regionName: "eu-central-1"), "http://localhost:12345");
        var imported = builder.AddAWSCloudFormationStack("imported", "existing-stack")
            .WithReference(builder.CreateResourceBuilder(localStack));

        await LocalStackConnectionStringAvailableCallback.CreateCallback(builder)(localStack, null!, CancellationToken.None);

        await Assert.That(imported.Resource.CloudFormationClient).IsNotNull();
        var configuration = imported.Resource.CloudFormationClient!.Config;
        await Assert.That(configuration.ProxyHost).IsEqualTo("localhost");
        await Assert.That(configuration.ProxyPort).IsEqualTo(12345);
        await Assert.That(configuration.RegionEndpoint.SystemName).IsEqualTo("eu-central-1");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Unrelated_Or_OtherSource_Destination_Is_Untouched(bool staleReverseReference)
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var first = new TestLocalStackResource("first", TestDataBuilders.CreateHostingState(), "http://localhost:12345");
        var other = new TestLocalStackResource("other", TestDataBuilders.CreateHostingState(), "http://localhost:12346");
        var worker = builder.AddResource(new ExecutableResource("worker", "not-run", "."))
            .WithReference(builder.CreateResourceBuilder(other));
        if (staleReverseReference)
        {
            first.Annotations.Add(new LocalStackReferenceAnnotation(worker.Resource));
        }

        await LocalStackConnectionStringAvailableCallback.CreateCallback(builder)(first, null!, CancellationToken.None);
        var environment = await EvaluateEnvironment(builder, worker.Resource);

        await Assert.That(environment.Keys.Count(key => key.StartsWith("LocalStack__", StringComparison.Ordinal))).IsEqualTo(0);
        await Assert.That(environment).ContainsKey("ConnectionStrings__other");
        await Assert.That(environment.ContainsKey("ConnectionStrings__first")).IsFalse();
    }

    [Test]
    public async Task Whole_Template_And_Sqs_Tool_Keep_Their_Specialized_Configuration()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var source = new TestLocalStackResource("localstack", TestDataBuilders.CreateHostingState(), "http://localhost:12345");
        var template = builder.AddAWSCloudFormationTemplate("template", "not-read.json")
            .WithReference(builder.CreateResourceBuilder(source));
        var tool = TestResourceFactory.CreateExecutableResourceByTypeName(Constants.SQSEventSourceResource, "sqs-helper");
        builder.CreateResourceBuilder(tool).WithReference(builder.CreateResourceBuilder(source));

        await LocalStackConnectionStringAvailableCallback.CreateCallback(builder)(source, null!, CancellationToken.None);
        var environment = await EvaluateEnvironment(builder, tool);

        await Assert.That(template.Resource.CloudFormationClient!.Config.ProxyHost).IsEqualTo("localhost");
        await Assert.That(template.Resource.CloudFormationClient.Config.ProxyPort).IsEqualTo(12345);
        await Assert.That(environment["AWS_ENDPOINT_URL"]).IsEqualTo("http://localhost:12345/");
        await Assert.That(environment.Keys.Count(key => key.StartsWith("LocalStack__", StringComparison.Ordinal))).IsEqualTo(0);
    }

    [Test]
    public async Task Manual_Environment_Added_After_Adaptation_Retains_Core_Precedence()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var source = new TestLocalStackResource("localstack", TestDataBuilders.CreateHostingState(), "http://localhost:12345");
        var worker = builder.AddResource(new ExecutableResource("worker", "not-run", "."))
            .WithReference(builder.CreateResourceBuilder(source));
        await LocalStackConnectionStringAvailableCallback.CreateCallback(builder)(source, null!, CancellationToken.None);
        worker.WithEnvironment("LocalStack__Session__RegionName", "eu-west-1");

        var environment = await EvaluateEnvironment(builder, worker.Resource);

        await Assert.That(environment["LocalStack__Session__RegionName"]).IsEqualTo("eu-west-1");
        await Assert.That(environment["LocalStack__Config__EdgePort"]).IsEqualTo("12345");
    }

    private static async Task<Dictionary<string, object>> EvaluateEnvironment(
        IDistributedApplicationBuilder builder, ExecutableResource resource)
    {
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        foreach (var (key, value) in context.EnvironmentVariables.ToArray())
        {
            if (value is IValueProvider provider)
            {
                context.EnvironmentVariables[key] = await provider.GetValueAsync(new ValueProviderContext
                {
                    ExecutionContext = builder.ExecutionContext,
                    Caller = resource,
                    Network = KnownNetworkIdentifiers.LocalhostNetwork,
                }) ?? string.Empty;
            }
        }

        return context.EnvironmentVariables;
    }

    private sealed class TestLocalStackResource(
        string name,
        LocalStackHostingState hostingState,
        string connectionString) : ILocalStackResource, ILocalStackHostingStateProvider, IResourceWithEndpoints
    {
        public string Name { get; } = name;

        public ResourceAnnotationCollection Annotations { get; } = CreateEndpointAnnotations(connectionString);

        public ReferenceExpression ConnectionStringExpression { get; } = ReferenceExpression.Create($"{connectionString}");

        public LocalStackHostingState HostingState { get; } = hostingState;

        private static ResourceAnnotationCollection CreateEndpointAnnotations(string connection)
        {
            var resource = new ContainerResource("test-endpoint");
            LocalStackEndpointEnvironmentEvaluator.AddEndpoint(resource, new Uri(connection));
            return resource.Annotations;
        }

#pragma warning disable CS0618
        public ILocalStackOptions Options => throw new NotSupportedException();
#pragma warning restore CS0618
    }
}
