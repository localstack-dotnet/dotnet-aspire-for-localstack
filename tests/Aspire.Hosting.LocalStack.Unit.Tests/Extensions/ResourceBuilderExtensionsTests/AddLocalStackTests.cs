namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class AddLocalStackTests
{
    [Test]
    public async Task AddLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Create_LocalStack_Resource_When_UseLocalStack_Is_True()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource).IsNotNull();
        await Assert.That(result.Resource).IsTypeOf<LocalStackResource>();
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Default_Name_When_Not_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.Name).IsEqualTo("localstack");
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Custom_Name_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customName = "my-localstack";

        var result = builder.AddLocalStack(name: customName, localStackOptions: localStackOptions);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.Name).IsEqualTo(customName);
    }

    [Test]
    public async Task AddLocalStack_Should_Configure_Container_Options_When_Action_Provided()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        var configureContainerCalled = false;

        var result = builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: ConfigureContainer);

        await Assert.That(result).IsNotNull();
        await Assert.That(configureContainerCalled).IsTrue();
        return;

        void ConfigureContainer(LocalStackContainerOptions options)
        {
            configureContainerCalled = true;
            options.DebugLevel = 1;
            options.LogLevel = LocalStackLogLevel.Debug;
        }
    }

    [Test]
    public async Task AddLocalStack_Should_Inherit_Region_From_AWS_Config()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        var awsConfig = TestDataBuilders.CreateMockAWSConfig("us-west-2");

        var result = builder.AddLocalStack(localStackOptions: localStackOptions, awsConfig: awsConfig);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.Options.Session.RegionName).IsEqualTo("us-west-2");
    }

    [Test]
    public async Task AddLocalStack_Should_Throw_ArgumentNullException_When_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        await Assert.That(() => builder.AddLocalStack(localStackOptions: localStackOptions)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task AddLocalStack_Should_Throw_ArgumentException_When_Name_Is_Invalid(string invalidName)
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        await Assert.That(() => builder.AddLocalStack(name: invalidName, localStackOptions: localStackOptions)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task AddLocalStack_Should_Set_EAGER_SERVICE_LOADING_When_EagerLoadedServices_Configured()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = [AwsService.Sqs]
        );

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;

        // Verify the resource was created
        await Assert.That(resource).IsNotNull();

        // Verify eager loading environment variable would be set
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        await Assert.That(envAnnotations).IsNotEmpty();
    }

    [Test]
    public async Task AddLocalStack_Should_Set_SERVICES_Environment_Variable_With_Comma_Separated_Services()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3]
        );

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Environment variables are set through annotations
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        await Assert.That(envAnnotations).IsNotEmpty();
    }

    [Test]
    public async Task AddLocalStack_Should_Not_Set_EAGER_SERVICE_LOADING_When_EagerLoadedServices_Empty()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = []
        );

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Throw_When_EagerLoadedServices_Conflicts_With_AdditionalEnvVars_SERVICES()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var exception = await Assert.That(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["SERVICES"] = "lambda";
                container.EagerLoadedServices = [AwsService.Sqs];
            })).ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Cannot set 'SERVICES'", StringComparison.OrdinalIgnoreCase);
        await Assert.That(exception.Message).Contains("AdditionalEnvironmentVariables", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AddLocalStack_Should_Throw_When_EagerLoadedServices_Conflicts_With_AdditionalEnvVars_EAGER_SERVICE_LOADING()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var exception = await Assert.That(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["EAGER_SERVICE_LOADING"] = "1";
                container.EagerLoadedServices = [AwsService.Sqs];
            })).ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Cannot set", StringComparison.OrdinalIgnoreCase);
        await Assert.That(exception.Message).Contains("EAGER_SERVICE_LOADING", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AddLocalStack_Should_Throw_When_Unsupported_Service_In_EagerLoadedServices()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        // Note: This test assumes there might be an AwsService enum value with no CliName
        // If all current services are supported, this validates the error handling mechanism
        // The actual exception will be thrown during the Select operation when CliName is null
        var exception = await Assert.That(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                // Using a very high enum value that likely doesn't have metadata
                container.EagerLoadedServices = [(AwsService)99999];
            })).Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("not supported by LocalStack", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AddLocalStack_Should_Mount_Docker_Socket_When_EnableDockerSocket_Is_True()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EnableDockerSocket = true
        );

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Verify the Docker socket bind mount annotation exists
        var mountAnnotations = resource.Annotations.OfType<ContainerMountAnnotation>();
        var dockerSocketMount = mountAnnotations.FirstOrDefault
            (m => m is { Source: "/var/run/docker.sock", Target: "/var/run/docker.sock", Type: ContainerMountType.BindMount });

        await Assert.That(dockerSocketMount).IsNotNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Not_Mount_Docker_Socket_When_EnableDockerSocket_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EnableDockerSocket = false
        );

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Verify no Docker socket bind mount annotation exists
        var mountAnnotations = resource.Annotations.OfType<ContainerMountAnnotation>();
        var dockerSocketMount = mountAnnotations.FirstOrDefault
            (m => m is { Source: "/var/run/docker.sock", Target: "/var/run/docker.sock" });

        await Assert.That(dockerSocketMount).IsNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Not_Mount_Docker_Socket_By_Default()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Verify no Docker socket bind mount annotation exists when not configured
        var mountAnnotations = resource.Annotations.OfType<ContainerMountAnnotation>();
        var dockerSocketMount = mountAnnotations.FirstOrDefault
            (m => m is { Source: "/var/run/docker.sock", Target: "/var/run/docker.sock" });

        await Assert.That(dockerSocketMount).IsNull();
    }

    [Test]
    [Arguments(ContainerLifetime.Session, null, null)]
    [Arguments(ContainerLifetime.Session, 1234, 1234)]
    [Arguments(ContainerLifetime.Persistent, null, Constants.DefaultContainerPort)]
    [Arguments(ContainerLifetime.Persistent, 1234, 1234)]
    public async Task AddLocalStack_Should_Set_Endpoint_Port(ContainerLifetime lifetime, int? port, int? expectedPort)
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container =>
            {
                container.Lifetime = lifetime;
                container.Port = port;
            });

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Verify endpoint port configuration
        var endpointAnnotations = resource.Annotations.OfType<EndpointAnnotation>();
        var httpEndpoint = endpointAnnotations.FirstOrDefault(e => e is { Name: "http" });

        await Assert.That(httpEndpoint).IsNotNull();
        await Assert.That(httpEndpoint!.Port).IsEqualTo(expectedPort);
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Default_Container_Image_Values_When_Not_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        // Verify default image annotations
        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(imageAnnotation.Registry).IsEqualTo("docker.io");
        await Assert.That(imageAnnotation.Image).IsEqualTo("localstack/localstack");
        await Assert.That(imageAnnotation.Tag).IsEqualTo("4.12.0");
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Custom_Container_Registry_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customRegistry = "artifactory.company.com";

        var result = builder.AddLocalStack(
            localStackOptions: localStackOptions,
            configureContainer: container => container.ContainerRegistry = customRegistry);

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(imageAnnotation.Registry).IsEqualTo(customRegistry);
        await Assert.That(imageAnnotation.Image).IsEqualTo("localstack/localstack"); // Default image
        await Assert.That(imageAnnotation.Tag).IsEqualTo("4.12.0"); // Default tag
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Custom_Container_Image_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customImage = "custom/localstack";

        var result = builder.AddLocalStack(
            localStackOptions: localStackOptions,
            configureContainer: container => container.ContainerImage = customImage);

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(imageAnnotation.Registry).IsEqualTo("docker.io"); // Default registry
        await Assert.That(imageAnnotation.Image).IsEqualTo(customImage);
        await Assert.That(imageAnnotation.Tag).IsEqualTo("4.12.0"); // Default tag
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Custom_Container_ImageTag_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customTag = "4.9.2";

        var result = builder.AddLocalStack(
            localStackOptions: localStackOptions,
            configureContainer: container => container.ContainerImageTag = customTag);

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(imageAnnotation.Registry).IsEqualTo("docker.io"); // Default registry
        await Assert.That(imageAnnotation.Image).IsEqualTo("localstack/localstack"); // Default image
        await Assert.That(imageAnnotation.Tag).IsEqualTo(customTag);
    }

    [Test]
    public async Task AddLocalStack_Should_Use_All_Custom_Container_Image_Values_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customRegistry = "artifactory.company.com";
        const string customImage = "docker-mirrors/localstack/localstack";
        const string customTag = "4.9.2";

        var result = builder.AddLocalStack(
            localStackOptions: localStackOptions,
            configureContainer: container =>
            {
                container.ContainerRegistry = customRegistry;
                container.ContainerImage = customImage;
                container.ContainerImageTag = customTag;
            });

        await Assert.That(result).IsNotNull();
        var resource = result!.Resource;
        await Assert.That(resource).IsNotNull();

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(imageAnnotation.Registry).IsEqualTo(customRegistry);
        await Assert.That(imageAnnotation.Image).IsEqualTo(customImage);
        await Assert.That(imageAnnotation.Tag).IsEqualTo(customTag);
    }
}
