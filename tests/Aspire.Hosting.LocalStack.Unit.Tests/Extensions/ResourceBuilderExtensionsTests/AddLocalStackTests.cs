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
}
