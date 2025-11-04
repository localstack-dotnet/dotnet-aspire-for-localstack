using LocalStack.Client.Enums;

#pragma warning disable S4143

namespace Aspire.Hosting.LocalStack.Unit.Tests.Container;

public class LocalStackContainerOptionsTests
{
    [Fact]
    public void Constructor_Should_Set_Correct_Defaults()
    {
        var options = new LocalStackContainerOptions();

        Assert.Equal(ContainerLifetime.Persistent, options.Lifetime);
        Assert.Equal(0, options.DebugLevel);
        Assert.Equal(LocalStackLogLevel.Error, options.LogLevel);
        Assert.NotNull(options.AdditionalEnvironmentVariables);
        Assert.Empty(options.AdditionalEnvironmentVariables);
        Assert.False(options.EnableDockerSocket);
    }

    [Fact]
    public void AdditionalEnvironmentVariables_Should_Be_Mutable_Dictionary()
    {
        var options = new LocalStackContainerOptions();

        options.AdditionalEnvironmentVariables["TEST_KEY"] = "test_value";
        options.AdditionalEnvironmentVariables["ANOTHER_KEY"] = "another_value";

        Assert.Equal(2, options.AdditionalEnvironmentVariables.Count);
        Assert.Equal("test_value", options.AdditionalEnvironmentVariables["TEST_KEY"]);
        Assert.Equal("another_value", options.AdditionalEnvironmentVariables["ANOTHER_KEY"]);
    }

    [Fact]
    public void AdditionalEnvironmentVariables_Should_Use_Ordinal_StringComparer()
    {
        var options = new LocalStackContainerOptions();

        options.AdditionalEnvironmentVariables["TestKey"] = "value1";
        options.AdditionalEnvironmentVariables["testkey"] = "value2";

        Assert.Equal(2, options.AdditionalEnvironmentVariables.Count);
        Assert.Equal("value1", options.AdditionalEnvironmentVariables["TestKey"]);
        Assert.Equal("value2", options.AdditionalEnvironmentVariables["testkey"]);
    }

    [Fact]
    public void EagerLoadedServices_Should_Default_To_Empty_Collection()
    {
        var options = new LocalStackContainerOptions();

        Assert.NotNull(options.EagerLoadedServices);
        Assert.Empty(options.EagerLoadedServices);
    }

    [Fact]
    public void EagerLoadedServices_Should_Accept_Single_Service()
    {
        var options = new LocalStackContainerOptions
        {
            EagerLoadedServices = [AwsService.Sqs],
        };

        Assert.Single(options.EagerLoadedServices);
        Assert.Contains(AwsService.Sqs, options.EagerLoadedServices);
    }

    [Fact]
    public void EagerLoadedServices_Should_Accept_Multiple_Services()
    {
        var options = new LocalStackContainerOptions
        {
            EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3],
        };

        Assert.Equal(3, options.EagerLoadedServices.Count);
        Assert.Contains(AwsService.Sqs, options.EagerLoadedServices);
        Assert.Contains(AwsService.DynamoDb, options.EagerLoadedServices);
        Assert.Contains(AwsService.S3, options.EagerLoadedServices);
    }

    [Fact]
    public void EnableDockerSocket_Should_Default_To_False()
    {
        var options = new LocalStackContainerOptions();

        Assert.False(options.EnableDockerSocket);
    }

    [Fact]
    public void EnableDockerSocket_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            EnableDockerSocket = true,
        };

        Assert.True(options.EnableDockerSocket);
    }

    [Fact]
    public void Port_Should_Default_To_Null()
    {
        var options = new LocalStackContainerOptions();

        Assert.Null(options.Port);
    }

    [Fact]
    public void Port_Should_Be_Settable()
    {
        var options = new LocalStackContainerOptions
        {
            Port = 1234,
        };

        Assert.Equal(1234, options.Port);
    }
}
