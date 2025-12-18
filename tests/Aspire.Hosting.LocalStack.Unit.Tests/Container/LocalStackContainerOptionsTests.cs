#pragma warning disable S4143

namespace Aspire.Hosting.LocalStack.Unit.Tests.Container;

public class LocalStackContainerOptionsTests
{
    [Test]
    public async Task Constructor_Should_Set_Correct_Defaults()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.Lifetime).IsEqualTo(ContainerLifetime.Session);
        await Assert.That(options.DebugLevel).IsEqualTo(0);
        await Assert.That(options.LogLevel).IsEqualTo(LocalStackLogLevel.Error);
        await Assert.That(options.AdditionalEnvironmentVariables).IsNotNull();
        await Assert.That(options.AdditionalEnvironmentVariables).IsEmpty();
        await Assert.That(options.EnableDockerSocket).IsFalse();
    }

    [Test]
    public async Task AdditionalEnvironmentVariables_Should_Be_Mutable_Dictionary()
    {
        var options = new LocalStackContainerOptions
        {
            AdditionalEnvironmentVariables =
            {
                ["TEST_KEY"] = "test_value",
                ["ANOTHER_KEY"] = "another_value"
            }
        };

        await Assert.That(options.AdditionalEnvironmentVariables.Count).IsEqualTo(2);
        await Assert.That(options.AdditionalEnvironmentVariables["TEST_KEY"]).IsEqualTo("test_value");
        await Assert.That(options.AdditionalEnvironmentVariables["ANOTHER_KEY"]).IsEqualTo("another_value");
    }

    [Test]
    public async Task AdditionalEnvironmentVariables_Should_Use_Ordinal_StringComparer()
    {
        var options = new LocalStackContainerOptions();

        options.AdditionalEnvironmentVariables["TestKey"] = "value1";
        options.AdditionalEnvironmentVariables["testkey"] = "value2";

        await Assert.That(options.AdditionalEnvironmentVariables.Count).IsEqualTo(2);
        await Assert.That(options.AdditionalEnvironmentVariables["TestKey"]).IsEqualTo("value1");
        await Assert.That(options.AdditionalEnvironmentVariables["testkey"]).IsEqualTo("value2");
    }

    [Test]
    public async Task EagerLoadedServices_Should_Default_To_Empty_Collection()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.EagerLoadedServices).IsNotNull();
        await Assert.That(options.EagerLoadedServices).IsEmpty();
    }

    [Test]
    public async Task EagerLoadedServices_Should_Accept_Single_Service()
    {
        var options = new LocalStackContainerOptions
        {
            EagerLoadedServices = [AwsService.Sqs],
        };

        await Assert.That(options.EagerLoadedServices).HasSingleItem();
        await Assert.That(options.EagerLoadedServices).Contains(AwsService.Sqs);
    }

    [Test]
    public async Task EagerLoadedServices_Should_Accept_Multiple_Services()
    {
        var options = new LocalStackContainerOptions
        {
            EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3],
        };

        await Assert.That(options.EagerLoadedServices.Count).IsEqualTo(3);
        await Assert.That(options.EagerLoadedServices).Contains(AwsService.Sqs);
        await Assert.That(options.EagerLoadedServices).Contains(AwsService.DynamoDb);
        await Assert.That(options.EagerLoadedServices).Contains(AwsService.S3);
    }

    [Test]
    public async Task EnableDockerSocket_Should_Default_To_False()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.EnableDockerSocket).IsFalse();
    }

    [Test]
    public async Task EnableDockerSocket_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            EnableDockerSocket = true,
        };

        await Assert.That(options.EnableDockerSocket).IsTrue();
    }

    [Test]
    public async Task Port_Should_Default_To_Null()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.Port).IsNull();
    }

    [Test]
    public async Task Port_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            Port = 1234,
        };

        await Assert.That(options.Port).IsEqualTo(1234);
    }

    [Test]
    public async Task ContainerRegistry_Should_Default_To_Null()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.ContainerRegistry).IsNull();
    }

    [Test]
    public async Task ContainerRegistry_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            ContainerRegistry = "artifactory.company.com",
        };

        await Assert.That(options.ContainerRegistry).IsEqualTo("artifactory.company.com");
    }

    [Test]
    public async Task ContainerImage_Should_Default_To_Null()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.ContainerImage).IsNull();
    }

    [Test]
    public async Task ContainerImage_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            ContainerImage = "custom/localstack",
        };

        await Assert.That(options.ContainerImage).IsEqualTo("custom/localstack");
    }

    [Test]
    public async Task ContainerImageTag_Should_Default_To_Null()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(options.ContainerImageTag).IsNull();
    }

    [Test]
    public async Task ContainerImageTag_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            ContainerImageTag = "4.9.2",
        };

        await Assert.That(options.ContainerImageTag).IsEqualTo("4.9.2");
    }
}
