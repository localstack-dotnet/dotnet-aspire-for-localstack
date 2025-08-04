namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class AddAWSCDKBootstrapCfTemplateForLocalStackTests
{
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
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);
        var localStackBuilder = builder.AddLocalStack(localStackOptions: localStackOptions);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        Assert.Null(result);
    }

    [Fact]
    public void AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Create_Template_When_LocalStack_Enabled()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var (localStackOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true);
        var localStackBuilder = builder.AddLocalStack(localStackOptions: localStackOptions);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        Assert.NotNull(result);
        Assert.NotNull(result.Resource);
        Assert.Equal("CDKBootstrap", result.Resource.Name);
    }
}
