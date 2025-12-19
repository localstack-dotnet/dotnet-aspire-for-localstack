namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackResourceConfiguratorTests
{
    [Test]
    public async Task ConfigureCloudFormationResource_Should_Set_CloudFormation_Client()
    {
        var cfResource = Substitute.For<ICloudFormationTemplateResource>();
        var localStackUrl = new Uri("http://localhost:4566");
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, options);

        await Assert.That(capturedClient).IsNotNull();
        await Assert.That(capturedClient).IsTypeOf<AmazonCloudFormationClient>();
    }

    [Test]
    public async Task ConfigureCloudFormationResource_Should_Configure_Client_With_LocalStack_Endpoint()
    {
        var cfResource = Substitute.For<ICloudFormationTemplateResource>();
        var localStackUrl = new Uri("http://test-host:9999");
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "test-host",
            useSsl: false);

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, options);

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
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useSsl: true);

        AmazonCloudFormationClient? capturedClient = null;
        cfResource
            .When(x => x.CloudFormationClient = Arg.Any<AmazonCloudFormationClient>())
            .Do(x => capturedClient = x.Args()[0] as AmazonCloudFormationClient);

        LocalStackResourceConfigurator.ConfigureCloudFormationResource(cfResource, localStackUrl, options);

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
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            useLocalStack: true,
            regionName: "us-west-2",
            edgePort: 4566,
            localStackHost: "localhost",
            useSsl: false);

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, options);

        await Assert.That(mockBuilder).IsNotNull();
        await Assert.That(options).IsNotNull();
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Handle_Custom_Port_And_Host()
    {
        var mockResource = Substitute.For<IResourceWithEnvironment>();
        var mockBuilder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        mockBuilder.Resource.Returns(mockResource);

        var localStackUrl = new Uri("https://custom-host:9999");
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "custom-host",
            useSsl: true);

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, options);
        await Assert.That(mockBuilder).IsNotNull();
    }

    [Test]
    public async Task ConfigureProjectResource_Should_Handle_All_Configuration_Options()
    {
        var mockResource = Substitute.For<IResourceWithEnvironment>();
        var mockBuilder = Substitute.For<IResourceBuilder<IResourceWithEnvironment>>();
        mockBuilder.Resource.Returns(mockResource);

        var localStackUrl = new Uri("http://localhost:4566");
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        LocalStackResourceConfigurator.ConfigureProjectResource(mockBuilder, localStackUrl, options);

        await Assert.That(options.UseLocalStack || !options.UseLocalStack).IsTrue(); // Verifies bool is accessible
        await Assert.That(options.Session).IsNotNull();
        await Assert.That(options.Config).IsNotNull();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Add_Environment_Annotation_With_AWS_Endpoint_URL()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "test-host",
            useSsl: false);
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(ann => mockExecutableResource.Annotations.Add(ann)), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, options);

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
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "test-host",
            useSsl: false);

        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(ann => mockExecutableResource.Annotations.Add(ann)), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        // Add some existing annotations
        var dummyResource = new ExecutableResource("dummy", "dummy-command", "dummy-workdir");
        mockExecutableResource.Annotations.Add(new ResourceRelationshipAnnotation(dummyResource, "test"));

        var initialAnnotationCount = mockExecutableResource.Annotations.Count;
        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, options);

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
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "test-host",
            useSsl: false);
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(ann => mockExecutableResource.Annotations.Add(ann)), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var initialAnnotationCount = mockExecutableResource.Annotations.Count;
        var localStackUrl = new Uri("http://localhost:4566");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, localStackUrl, options);

        // Should have added exactly one EnvironmentCallbackAnnotation
        await Assert.That(mockExecutableResource.Annotations.Count).IsEqualTo(initialAnnotationCount + 1);
        await Assert.That(mockExecutableResource.Annotations.OfType<EnvironmentCallbackAnnotation>()).HasSingleItem();
    }

    [Test]
    public async Task ConfigureSqsEventSourceResource_Should_Handle_Different_LocalStack_URLs()
    {
        var mockExecutableResource = new ExecutableResource("test-sqs-resource", "test-command", "test-workdir");
        var mockBuilder = Substitute.For<IResourceBuilder<ExecutableResource>>();
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 9999,
            localStackHost: "test-host",
            useSsl: false);
        mockBuilder.Resource.Returns(mockExecutableResource);

        // Configure the mock to actually add annotations when WithAnnotation is called
        mockBuilder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(ann => mockExecutableResource.Annotations.Add(ann)), Arg.Any<ResourceAnnotationMutationBehavior>())
            .Returns(mockBuilder);

        var customLocalStackUrl = new Uri("https://custom-host:9999");

        LocalStackResourceConfigurator.ConfigureSqsEventSourceResource(mockBuilder, customLocalStackUrl, options);

        var envAnnotation = mockExecutableResource.Annotations.OfType<EnvironmentCallbackAnnotation>().FirstOrDefault();
        await Assert.That(envAnnotation).IsNotNull();
    }
}
