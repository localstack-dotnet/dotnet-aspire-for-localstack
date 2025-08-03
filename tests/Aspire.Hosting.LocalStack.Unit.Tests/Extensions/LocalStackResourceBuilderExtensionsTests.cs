using LocalStack.Client.Options;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions;

public class LocalStackResourceBuilderExtensionsTests
{
    [Fact]
    public void AddLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: false);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.Null(result);
    }

    [Fact]
    public void AddLocalStack_Should_Create_LocalStack_Resource_When_UseLocalStack_Is_True()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.NotNull(result.Resource);
        Assert.IsType<LocalStackResource>(result.Resource);
    }

    [Fact]
    public void AddLocalStack_Should_Use_Default_Name_When_Not_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.Equal("localstack", result.Resource.Name);
    }

    [Fact]
    public void AddLocalStack_Should_Use_Custom_Name_When_Specified()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);
        const string customName = "my-localstack";

        var result = builder.AddLocalStack(name: customName, localStackOptions: localStackOptions);

        Assert.NotNull(result);
        Assert.Equal(customName, result.Resource.Name);
    }

    [Fact]
    public void AddLocalStack_Should_Configure_Container_Options_When_Action_Provided()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);

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
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);

        var awsConfig = Substitute.For<IAWSSDKConfig>();
        awsConfig.Region.Returns(Amazon.RegionEndpoint.USWest2);

        var result = builder.AddLocalStack(localStackOptions: localStackOptions, awsConfig: awsConfig);

        Assert.NotNull(result);
        Assert.Equal("us-west-2", result.Resource.Options.Session.RegionName);
    }

    [Fact]
    public void UseLocalStack_Should_Return_Builder_When_LocalStack_Is_Null()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.UseLocalStack(localStack: null);

        Assert.Same(builder, result);
    }

    [Fact]
    public void UseLocalStack_Should_Return_Builder_When_LocalStack_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = Substitute.For<ILocalStackOptions>();
        localStackOptions.UseLocalStack.Returns(returnThis: false);
        var localStackBuilder = builder.AddLocalStack(localStackOptions: localStackOptions);

        var result = builder.UseLocalStack(localStackBuilder);

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Return_Null_When_LocalStack_Is_Null()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder: null);

        Assert.Null(result);
    }

    [Fact]
    public void AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = Substitute.For<ILocalStackOptions>();
        localStackOptions.UseLocalStack.Returns(returnThis: false);
        var localStackBuilder = builder.AddLocalStack(localStackOptions: localStackOptions);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        Assert.Null(result);
    }

    [Fact]
    public void AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Create_Template_When_LocalStack_Enabled()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackOptions = CreateMockLocalStackOptions(useLocalStack: true);
        var localStackBuilder = builder.AddLocalStack(localStackOptions: localStackOptions);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        Assert.NotNull(result);
        Assert.NotNull(result.Resource);
        Assert.Equal("CDKBootstrap", result.Resource.Name);
    }

    private static ILocalStackOptions CreateMockLocalStackOptions(bool useLocalStack = true)
    {
        var mockOptions = Substitute.For<ILocalStackOptions>();

        var configOptions = new ConfigOptions("localhost");
        var sessionOptions = new SessionOptions("test", "test", "test");

        mockOptions.UseLocalStack.Returns(useLocalStack);
        mockOptions.Config.Returns(configOptions);
        mockOptions.Session.Returns(sessionOptions);

        return mockOptions;
    }
}
