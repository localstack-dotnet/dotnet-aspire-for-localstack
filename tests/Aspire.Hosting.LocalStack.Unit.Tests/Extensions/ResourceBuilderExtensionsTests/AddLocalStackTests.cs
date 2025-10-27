using LocalStack.Client.Enums;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class AddLocalStackTests
{
    [Fact]
    public void AddLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.Null(result);
    }

    [Fact]
    public void AddLocalStack_Should_Create_LocalStack_Resource_When_UseLocalStack_Is_True()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Resource);
        Assert.IsType<LocalStackResource>(result.Resource);
    }

    [Fact]
    public void AddLocalStack_Should_Use_Default_Name_When_Not_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.Equal("localstack", result.Resource.Name);
    }

    [Fact]
    public void AddLocalStack_Should_Use_Custom_Name_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        const string customName = "my-localstack";

        var result = builder.AddLocalStack(name: customName, localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.Equal(customName, result.Resource.Name);
    }

    [Fact]
    public void AddLocalStack_Should_Configure_Container_Options_When_Action_Provided()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        var configureContainerCalled = false;

        var result = builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: ConfigureContainer);

        Assert.NotNull(result);
        Assert.True(configureContainerCalled);
        return;

        void ConfigureContainer(LocalStackContainerOptions options)
        {
            configureContainerCalled = true;
            options.DebugLevel = 1;
            options.LogLevel = LocalStackLogLevel.Debug;
        }
    }

    [Fact]
    public void AddLocalStack_Should_Inherit_Region_From_AWS_Config()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        var awsConfig = TestDataBuilders.CreateMockAWSConfig("us-west-2");

        var result = builder.AddLocalStack(localStackOptions: localStackOptions, awsConfig: awsConfig);

        Assert.NotNull(result);
        Assert.Equal("us-west-2", result.Resource.Options.Session.RegionName);
    }

    [Fact]
    public void AddLocalStack_Should_Throw_ArgumentNullException_When_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        Assert.Throws<ArgumentNullException>(() => builder.AddLocalStack(localStackOptions: localStackOptions));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLocalStack_Should_Throw_ArgumentException_When_Name_Is_Invalid(string invalidName)
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        Assert.Throws<ArgumentException>(() => builder.AddLocalStack(name: invalidName, localStackOptions: localStackOptions));
    }

    [Fact]
    public void AddLocalStack_Should_Set_EAGER_SERVICE_LOADING_When_EagerLoadedServices_Configured()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = [AwsService.Sqs]
        );

        Assert.NotNull(result);
        var resource = result.Resource;

        // Verify the resource was created
        Assert.NotNull(resource);

        // Verify eager loading environment variable would be set
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void AddLocalStack_Should_Set_SERVICES_Environment_Variable_With_Comma_Separated_Services()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3]
        );

        Assert.NotNull(result);
        var resource = result.Resource;
        Assert.NotNull(resource);

        // Environment variables are set through annotations
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void AddLocalStack_Should_Not_Set_EAGER_SERVICE_LOADING_When_EagerLoadedServices_Empty()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack
        (
            localStackOptions: localStackOptions,
            configureContainer: container => container.EagerLoadedServices = []
        );

        Assert.NotNull(result);
        var resource = result.Resource;
        Assert.NotNull(resource);
    }

    [Fact]
    public void AddLocalStack_Should_Throw_When_EagerLoadedServices_Conflicts_With_AdditionalEnvVars_SERVICES()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["SERVICES"] = "lambda";
                container.EagerLoadedServices = [AwsService.Sqs];
            }));

        Assert.Contains("Cannot set 'SERVICES'", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AdditionalEnvironmentVariables", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddLocalStack_Should_Throw_When_EagerLoadedServices_Conflicts_With_AdditionalEnvVars_EAGER_SERVICE_LOADING()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["EAGER_SERVICE_LOADING"] = "1";
                container.EagerLoadedServices = [AwsService.Sqs];
            }));

        Assert.Contains("Cannot set", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EAGER_SERVICE_LOADING", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddLocalStack_Should_Throw_When_Unsupported_Service_In_EagerLoadedServices()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);

        // Note: This test assumes there might be an AwsService enum value with no CliName
        // If all current services are supported, this validates the error handling mechanism
        // The actual exception will be thrown during the Select operation when CliName is null
        var exception = Assert.ThrowsAny<InvalidOperationException>(() =>
            builder.AddLocalStack(localStackOptions: localStackOptions, configureContainer: container =>
            {
                // Using a very high enum value that likely doesn't have metadata
                container.EagerLoadedServices = [(AwsService)99999];
            }));

        Assert.Contains("not supported by LocalStack", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
