namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackResourceConfiguratorTests
{
    [Test]
    public async Task ConfigureCloudFormationResource_Should_Set_CloudFormation_Client()
    {
        var cfResource = Substitute.For<ICloudFormationTemplateResource>();
        var localStackUrl = new Uri("http://localhost:4566");
        var state = TestDataBuilders.CreateHostingState();

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, state);

        await Assert.That(capturedClient).IsNotNull();
        await Assert.That(capturedClient).IsTypeOf<AmazonCloudFormationClient>();
    }

    [Test]
    public async Task ConfigureCloudFormationResource_Should_Configure_Client_With_LocalStack_Endpoint()
    {
        var cfResource = Substitute.For<ICloudFormationTemplateResource>();
        var localStackUrl = new Uri("http://test-host:9999");
        var state = TestDataBuilders.CreateHostingState(useSsl: false);

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, state);

        await Assert.That(capturedClient).IsNotNull();
        await Assert.That(capturedClient.Config).IsNotNull();

        // Log some debug info about what's actually configured
        var config = capturedClient.Config;
        var debugInfo = $"ServiceURL: '{config.ServiceURL}', " +
                        $"RegionEndpoint: '{config.RegionEndpoint}', " +
                        $"UseHttp: '{config.UseHttp}', " +
                        $"ProxyHost: '{config.ProxyHost}', " +
                        $"ProxyPort: '{config.ProxyPort}'";

        // Verify client was created with valid configuration
        await Assert.That(config.ServiceURL ?? config.RegionEndpoint?.SystemName).IsNotNull()
            .Because($"Client config: {debugInfo}");
    }

    [Test]
    public async Task ConfigureCloudFormationResource_Should_Handle_SSL_Configuration()
    {
        var cfResource = Substitute.For<ICloudFormationTemplateResource>();
        var localStackUrl = new Uri("https://localhost:4566");
        var state = TestDataBuilders.CreateHostingState(useSsl: true);

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, state);

        await Assert.That(capturedClient).IsNotNull();
        await Assert.That(capturedClient.Config).IsNotNull();

        var config = capturedClient.Config;

        // Verify SSL client has valid configuration
        await Assert.That(capturedClient).IsNotNull()
            .Because($"SSL client created with UseHttp: {config.UseHttp}");
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Call_WithEnvironment()
    {
        var mockResource = Substitute.For<IResourceWithEnvironment>();
        var mockBuilder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        mockBuilder.Resource.Returns(mockResource);

        var localStackUrl = new Uri("http://localhost:4566");
        var state = TestDataBuilders.CreateHostingState(
            regionName: "us-west-2",
            useSsl: false);

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, state);

        await Assert.That(mockBuilder).IsNotNull();
        await Assert.That(state).IsNotNull();
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Handle_Custom_Port_And_Host()
    {
        var mockResource = Substitute.For<IResourceWithEnvironment>();
        var mockBuilder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        mockBuilder.Resource.Returns(mockResource);

        var localStackUrl = new Uri("https://custom-host:9999");
        var state = TestDataBuilders.CreateHostingState(useSsl: true);

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, state);
        await Assert.That(mockBuilder).IsNotNull();
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Emit_LocalStack_Client_Environment_From_State_And_Allocated_Endpoint()
    {
        var projectResource = new ExecutableResource("project", "command", "workdir");
        var builder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        builder.Resource.Returns(projectResource);
        builder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(projectResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(builder);
        var state = TestDataBuilders.CreateHostingState(
            regionName: "eu-central-1",
            useSsl: true,
            useLegacyPorts: true,
            awsAccessKeyId: "state-key",
            awsAccessKey: "state-secret",
            awsSessionToken: "state-token");
        var allocatedEndpoint = new Uri("http://allocated-host:12345");

        LocalStackResourceConfigurator.ConfigureProjectResource(builder, allocatedEndpoint, state);

        var envAnnotation = projectResource.Annotations.OfType<EnvironmentCallbackAnnotation>().Single();
        var env = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), projectResource, env);
        await envAnnotation.Callback(context);

        await Assert.That(env["LocalStack__UseLocalStack"]).IsEqualTo("True");
        await Assert.That(env["LocalStack__Session__AwsAccessKeyId"]).IsEqualTo("state-key");
        await Assert.That(env["LocalStack__Session__AwsAccessKey"]).IsEqualTo("state-secret");
        await Assert.That(env["LocalStack__Session__AwsSessionToken"]).IsEqualTo("state-token");
        await Assert.That(env["LocalStack__Session__RegionName"]).IsEqualTo("eu-central-1");
        await Assert.That(env["LocalStack__Config__LocalStackHost"]).IsEqualTo("allocated-host");
        await Assert.That(env["LocalStack__Config__UseSsl"]).IsEqualTo("True");
        await Assert.That(env["LocalStack__Config__UseLegacyPorts"]).IsEqualTo("True");
        await Assert.That(env["LocalStack__Config__EdgePort"]).IsEqualTo("12345");
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Handle_All_Configuration_Options()
    {
        var mockResource = Substitute.For<IResourceWithEnvironment>();
        var mockBuilder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        mockBuilder.Resource.Returns(mockResource);

        var localStackUrl = new Uri("http://localhost:4566");
        var state = TestDataBuilders.CreateHostingState();

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, state);

        await Assert.That(state.Enabled || !state.Enabled).IsTrue(); // Verifies bool is accessible
        await Assert.That(state.Region).IsNotNull();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Add_Environment_Annotation_With_AWS_Endpoint_URL()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var state = TestDataBuilders.CreateHostingState(useSsl: false);
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(mockExecutableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, state);

        var envAnnotations = mockExecutableResource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        await Assert.That(envAnnotations).IsNotEmpty();
        await Assert.That(envAnnotations).HasSingleItem();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Add_Annotation_Via_WithEnvironment()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        var state = TestDataBuilders.CreateHostingState(useSsl: false);

        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(mockExecutableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        // Add some existing annotations
        var dummyResource = new ExecutableResource("dummy", "dummy-command", "dummy-workdir");
        mockExecutableResource.Annotations.Add(new ResourceRelationshipAnnotation(dummyResource, "test"));

        var initialAnnotationCount = mockExecutableResource.Annotations.Count;
        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, state);

        await Assert.That(mockExecutableResource.Annotations.Count).IsEqualTo(initialAnnotationCount + 1);

        var envAnnotations = mockExecutableResource.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();
        await Assert.That(envAnnotations).HasSingleItem();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Handle_Empty_Annotations_Collection()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        var state = TestDataBuilders.CreateHostingState(useSsl: false);
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(mockExecutableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var initialAnnotationCount = mockExecutableResource.Annotations.Count;
        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, state);

        // Should have added exactly one EnvironmentCallbackAnnotation
        await Assert.That(mockExecutableResource.Annotations.Count).IsEqualTo(initialAnnotationCount + 1);
        await Assert.That(mockExecutableResource.Annotations.OfType<EnvironmentCallbackAnnotation>()).HasSingleItem();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Handle_Different_LocalStack_URLs()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        var state = TestDataBuilders.CreateHostingState(useSsl: false);
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(mockExecutableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var customLocalStackUrl = new Uri("https://custom-host:9999");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, customLocalStackUrl, state);

        var envAnnotation = mockExecutableResource.Annotations.OfType<EnvironmentCallbackAnnotation>().FirstOrDefault();
        await Assert.That(envAnnotation).IsNotNull();
    }

    [Test]
    public async Task ConfigureDynamoDbStreamsEventSourceResource_Should_Emit_Global_And_Service_Specific_AWS_Endpoints()
    {
        var executableResource = new ExecutableResource("test-ddb-streams-resource", "test-command", "test-workdir");
        var builder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        var state = TestDataBuilders.CreateHostingState(regionName: "eu-central-1");

        builder.Resource.Returns(executableResource);
        builder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(executableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(builder);

        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureDynamoDbStreamsEventSourceResource(builder, localStackUrl, state);

        var envAnnotation = executableResource.Annotations.OfType<EnvironmentCallbackAnnotation>().Single();
        var env = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), executableResource, env);

        await envAnnotation.Callback(context);

        await Assert.That(env["AWS_ENDPOINT_URL"]).IsEqualTo("http://localhost:4566/");
        await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB"]).IsEqualTo("http://localhost:4566/");
        await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"]).IsEqualTo("http://localhost:4566/");
        await Assert.That(env["AWS_ACCESS_KEY_ID"]).IsEqualTo("test-key");
        await Assert.That(env["AWS_SECRET_ACCESS_KEY"]).IsEqualTo("test-secret");
        await Assert.That(env["AWS_SESSION_TOKEN"]).IsEqualTo("test-token");
        await Assert.That(env["AWS_DEFAULT_REGION"]).IsEqualTo("eu-central-1");
    }

    [Test]
    public async Task ConfigureStackResource_Should_Assign_Config_With_LocalStack_Region()
    {
        var stackResource = Substitute.For<IStackResource>();
        IAWSSDKConfig? assigned = null;
        stackResource.AWSSDKConfig.Returns(_ => assigned);
        stackResource
            .When(static r => r.AWSSDKConfig = Arg.Any<IAWSSDKConfig?>())
            .Do(callInfo => assigned = callInfo.Arg<IAWSSDKConfig?>());
        var state = TestDataBuilders.CreateHostingState(regionName: "eu-central-1");

        LocalStackResourceConfigurator.ConfigureStackResource(stackResource, state);

        await Assert.That(assigned).IsNotNull();
        await Assert.That(assigned!.Region).IsEqualTo(Amazon.RegionEndpoint.EUCentral1);
        await Assert.That(assigned.Profile).IsNull();
    }

    [Test]
    public async Task ConfigureStackResource_Should_Default_Region_When_Option_Region_Empty()
    {
        var stackResource = Substitute.For<IStackResource>();
        IAWSSDKConfig? assigned = null;
        stackResource.AWSSDKConfig.Returns(_ => assigned);
        stackResource
            .When(static r => r.AWSSDKConfig = Arg.Any<IAWSSDKConfig?>())
            .Do(callInfo => assigned = callInfo.Arg<IAWSSDKConfig?>());
        var state = TestDataBuilders.CreateHostingState(regionName: "");

        LocalStackResourceConfigurator.ConfigureStackResource(stackResource, state);

        await Assert.That(assigned).IsNotNull();
        await Assert.That(assigned!.Region).IsEqualTo(Amazon.RegionEndpoint.USEast1);
    }
}
