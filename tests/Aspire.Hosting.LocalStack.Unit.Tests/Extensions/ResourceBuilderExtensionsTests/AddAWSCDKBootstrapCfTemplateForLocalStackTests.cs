namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class AddAWSCDKBootstrapCfTemplateForLocalStackTests
{
    [Test]
    public async Task AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Return_Null_When_LocalStack_Is_Null()
    {
        var builder = DistributedApplication.CreateBuilder([]);

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder: null);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Return_Null_When_UseLocalStack_Is_False()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackBuilder = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(false));

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task AddAWSCDKBootstrapCloudFormationTemplateForLocalStack_Should_Create_Template_When_LocalStack_Enabled()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var localStackBuilder = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));

        var result = builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStackBuilder);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Resource).IsNotNull();
        await Assert.That(result.Resource.Name).IsEqualTo("CDKBootstrap");
    }
}
