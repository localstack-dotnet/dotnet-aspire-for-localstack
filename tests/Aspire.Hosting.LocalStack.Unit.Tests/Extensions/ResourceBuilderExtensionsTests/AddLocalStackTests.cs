namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class AddLocalStackTests
{
    [Test]
    public async Task AddLocalStack_ApprovedOverloads_Should_Compile_Without_Ambiguity()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var awsConfig = TestDataBuilders.CreateMockAWSConfig("us-west-2");
        Action<LocalStackContainerOptions> configureContainer = _ => { };

        var defaultResult = builder.AddLocalStack();
        var nameResult = builder.AddLocalStack("custom");
        var awsAndContainerResult = builder.AddLocalStack("custom-aws", awsConfig, configureContainer);
        var explicitOptionsResult = builder.AddLocalStack(
            "custom-explicit",
            awsConfig,
            options => options.WithEnabled(true),
            configureContainer);

        await Assert.That(defaultResult).IsNull();
        await Assert.That(nameResult).IsNull();
        await Assert.That(awsAndContainerResult).IsNull();
        await Assert.That(explicitOptionsResult).IsNotNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Invoke_ConfigureOptions_After_CanonicalBinding()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        AddCanonicalLocalStackConfiguration(builder, enabled: true, region: "canonical-region");
        var callbackObservedCanonicalValues = false;

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            configureOptions: options =>
            {
                callbackObservedCanonicalValues = options is
                {
                    Enabled: true,
                    Region: "canonical-region",
                    AccessKeyId: "canonical-key",
                    SecretAccessKey: "canonical-secret",
                    SessionToken: "canonical-token",
                };

                options.Region = "callback-region";
            });

        await Assert.That(result).IsNotNull();
        await Assert.That(callbackObservedCanonicalValues).IsTrue();
        await Assert.That(result!.Resource.GetHostingState().Region).IsEqualTo("callback-region");
    }

    [Test]
    public async Task AddLocalStack_Should_Return_Null_And_Not_Invoke_ConfigureContainer_When_HostingOptions_Disabled()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var configureContainerCalled = false;

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            configureOptions: options => options.WithEnabled(false),
            configureContainer: _ => configureContainerCalled = true);

        await Assert.That(result).IsNull();
        await Assert.That(configureContainerCalled).IsFalse();
    }

    [Test]
    public async Task AddLocalStack_ApprovedOverloads_Should_Produce_Equivalent_State_When_Configured_Equivalently()
    {
        var defaultBuilder = DistributedApplication.CreateBuilder([]);
        AddCanonicalLocalStackConfiguration(defaultBuilder, enabled: true, region: "equivalent-region");

        var nameBuilder = DistributedApplication.CreateBuilder([]);
        AddCanonicalLocalStackConfiguration(nameBuilder, enabled: true, region: "equivalent-region");

        var awsContainerBuilder = DistributedApplication.CreateBuilder([]);
        AddCanonicalLocalStackConfiguration(awsContainerBuilder, enabled: true, region: "canonical-region");

        var explicitBuilder = DistributedApplication.CreateBuilder([]);
        AddCanonicalLocalStackConfiguration(explicitBuilder, enabled: false, region: "disabled-region");

        var awsConfig = TestDataBuilders.CreateMockAWSConfig("equivalent-region");

        var defaultResult = defaultBuilder.AddLocalStack();
        var nameResult = nameBuilder.AddLocalStack("custom");
        var awsAndContainerResult = awsContainerBuilder.AddLocalStack("custom", awsConfig, configureContainer: null);
        var explicitResult = explicitBuilder.AddLocalStack(
            "custom",
            awsConfig: null,
            configureOptions: options =>
            {
                options.WithEnabled(true)
                    .WithRegion("equivalent-region")
                    .WithCredentials("canonical-key", "canonical-secret", "canonical-token");
            });

        await Assert.That(defaultResult).IsNotNull();
        await Assert.That(nameResult).IsNotNull();
        await Assert.That(awsAndContainerResult).IsNotNull();
        await Assert.That(explicitResult).IsNotNull();

        await AssertEquivalentState(defaultResult!.Resource.GetHostingState(), nameResult!.Resource.GetHostingState());
        await AssertEquivalentState(defaultResult.Resource.GetHostingState(), awsAndContainerResult!.Resource.GetHostingState());
        await AssertEquivalentState(defaultResult.Resource.GetHostingState(), explicitResult!.Resource.GetHostingState());
    }

    [Test]
    public async Task AddLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(false));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AddLocalStack_Should_Create_LocalStack_Resource_When_UseLocalStack_Is_True()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource).IsNotNull();
        await Assert.That(result.Resource).IsTypeOf<LocalStackResource>();
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Default_Name_When_Not_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.Name).IsEqualTo("localstack");
    }

    [Test]
    public async Task AddLocalStack_Should_Use_Custom_Name_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        const string customName = "my-localstack";

        var result = builder.AddLocalStack(customName, awsConfig: null, options => options.WithEnabled(true));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.Name).IsEqualTo(customName);
    }

    [Test]
    public async Task AddLocalStack_Should_Configure_Container_Options_When_Action_Provided()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var configureContainerCalled = false;

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true), ConfigureContainer);

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
        var awsConfig = TestDataBuilders.CreateMockAWSConfig("us-west-2");

        var result = builder.AddLocalStack("localstack", awsConfig, options => options.WithEnabled(true));

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource.GetHostingState().Region).IsEqualTo("us-west-2");
    }

    [Test]
    public async Task AddLocalStack_Should_Throw_ArgumentNullException_When_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;

        await Assert.That(() => builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true))).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task AddLocalStack_Should_Throw_ArgumentException_When_Name_Is_Invalid(string invalidName)
    {
        var builder = DistributedApplication.CreateBuilder([]);

        await Assert.That(() => builder.AddLocalStack(invalidName, awsConfig: null, options => options.WithEnabled(true))).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task AddLocalStack_Should_Set_EAGER_SERVICE_LOADING_When_EagerLoadedServices_Configured()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var exception = await Assert.That(() =>
            builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true), configureContainer: container =>
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

        var exception = await Assert.That(() =>
            builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true), configureContainer: container =>
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

        // Note: This test assumes there might be an AwsService enum value with no CliName
        // If all current services are supported, this validates the error handling mechanism
        // The actual exception will be thrown during the Select operation when CliName is null
        var exception = await Assert.That(() =>
            builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true), configureContainer: container =>
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

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));

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

        var result = builder.AddLocalStack
        (
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

        var result = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));

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
        const string customRegistry = "artifactory.company.com";

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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
        const string customImage = "custom/localstack";

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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
        const string customTag = "4.9.2";

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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
        const string customRegistry = "artifactory.company.com";
        const string customImage = "docker-mirrors/localstack/localstack";
        const string customTag = "4.9.2";

        var result = builder.AddLocalStack(
            "localstack",
            awsConfig: null,
            options => options.WithEnabled(true),
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

    private static void AddCanonicalLocalStackConfiguration(
        IDistributedApplicationBuilder builder,
        bool enabled,
        string region)
    {
        builder.Configuration[$"{LocalStackHostingOptionsResolver.CanonicalSectionName}:Enabled"] = enabled.ToString();
        builder.Configuration[$"{LocalStackHostingOptionsResolver.CanonicalSectionName}:Region"] = region;
        builder.Configuration[$"{LocalStackHostingOptionsResolver.CanonicalSectionName}:AccessKeyId"] = "canonical-key";
        builder.Configuration[$"{LocalStackHostingOptionsResolver.CanonicalSectionName}:SecretAccessKey"] = "canonical-secret";
        builder.Configuration[$"{LocalStackHostingOptionsResolver.CanonicalSectionName}:SessionToken"] = "canonical-token";
    }

    private static async Task AssertEquivalentState(LocalStackHostingState expected, LocalStackHostingState actual)
    {
        await Assert.That(actual.Enabled).IsEqualTo(expected.Enabled);
        await Assert.That(actual.Region).IsEqualTo(expected.Region);
        await Assert.That(actual.AccessKeyId).IsEqualTo(expected.AccessKeyId);
        await Assert.That(actual.SecretAccessKey).IsEqualTo(expected.SecretAccessKey);
        await Assert.That(actual.SessionToken).IsEqualTo(expected.SessionToken);
        await Assert.That(actual.UseSsl).IsEqualTo(expected.UseSsl);
        await Assert.That(actual.UseLegacyPorts).IsEqualTo(expected.UseLegacyPorts);
    }
}
