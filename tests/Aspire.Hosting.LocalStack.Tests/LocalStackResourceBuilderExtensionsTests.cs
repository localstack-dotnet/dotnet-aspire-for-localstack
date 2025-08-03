using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS;
using Aspire.Hosting.LocalStack;
using Aspire.Hosting.LocalStack.Configuration;
using Aspire.Hosting.LocalStack.Container;
using LocalStack.Client.Contracts;
using LocalStack.Client.Options;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace Aspire.Hosting.LocalStack.Tests;

internal class LocalStackResourceBuilderExtensionsTests
{
    private static IDistributedApplicationBuilder CreateBuilderWithLocalStackConfig(bool useLocalStack = true)
    {
        var configData = new Dictionary<string, string?>
        {
            ["LocalStack:UseLocalStack"] = useLocalStack.ToString(),
            ["LocalStack:Config:EdgePort"] = "4566",
            ["LocalStack:Session:RegionName"] = "us-east-1"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return TestingExtensions.CreateTestDistributedApplicationBuilder(configuration);
    }

    [Fact]
    public void AddLocalStackWithDefaultParametersWhenLocalStackEnabledShouldReturnLocalStackResourceBuilder()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);

        // Act
        var result = builder.AddLocalStack();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IResourceBuilder<ILocalStackResource>>(result);
        Assert.Equal("localstack", result.Resource.Name);
        Assert.True(result.Resource.Options.UseLocalStack);
    }

    [Fact]
    public void AddLocalStackWithCustomNameShouldUseProvidedName()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);
        const string customName = "my-localstack";

        // Act
        var result = builder.AddLocalStack(customName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(customName, result.Resource.Name);
    }

    [Fact]
    public void AddLocalStackWithUseLocalStackFalseShouldReturnNull()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: false);

        // Act
        var result = builder.AddLocalStack();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void AddLocalStackWithCustomOptionsShouldUseProvidedOptions()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: false); // Default config disabled
        var customOptions = new LocalStackOptions()
            .WithUseLocalStack(true)
            .WithRegion("eu-west-1")
            .WithEdgePort(4567);

        // Act
        var result = builder.AddLocalStack(localStackOptions: customOptions);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Resource.Options.UseLocalStack);
        Assert.Equal("eu-west-1", result.Resource.Options.Session.RegionName);
        Assert.Equal(4567, result.Resource.Options.Config.EdgePort);
    }

    [Fact]
    public void AddLocalStackWithAwsConfigShouldInheritRegion()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);
        var awsConfig = Substitute.For<IAWSSDKConfig>();
        var mockRegion = Substitute.For<IRegionEndpoint>();
        mockRegion.SystemName.Returns("ap-southeast-2");
        awsConfig.Region.Returns(mockRegion);

        // Act
        var result = builder.AddLocalStack(awsConfig: awsConfig);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ap-southeast-2", result.Resource.Options.Session.RegionName);
    }

    [Fact]
    public void AddLocalStackWithContainerConfigurationShouldApplyConfiguration()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);
        var containerConfigCalled = false;
        LocalStackContainerOptions? capturedOptions = null;

        // Act
        var result = builder.AddLocalStack(configureContainer: options =>
        {
            containerConfigCalled = true;
            capturedOptions = options;
            options.Lifetime = ResourceLifetime.Session;
            options.LogLevel = LocalStackLogLevel.Debug;
            options.DebugLevel = 2;
        });

        // Assert
        Assert.NotNull(result);
        Assert.True(containerConfigCalled);
        Assert.NotNull(capturedOptions);
        Assert.Equal(ResourceLifetime.Session, capturedOptions.Lifetime);
        Assert.Equal(LocalStackLogLevel.Debug, capturedOptions.LogLevel);
        Assert.Equal(2, capturedOptions.DebugLevel);
    }

    [Fact]
    public void AddLocalStackOptionsWithValidConfigurationShouldReturnConfiguredOptions()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);

        // Act
        var options = builder.AddLocalStackOptions();

        // Assert
        Assert.NotNull(options);
        Assert.True(options.UseLocalStack);
        Assert.Equal(4566, options.Config.EdgePort);
        Assert.Equal("us-east-1", options.Session.RegionName);
    }

    [Fact]
    public void AddLocalStackOptionsWithNoConfigurationShouldReturnDefaultOptionsWithUseLocalStackFalse()
    {
        // Arrange
        var builder = TestingExtensions.CreateTestDistributedApplicationBuilder();

        // Act
        var options = builder.AddLocalStackOptions();

        // Assert
        Assert.NotNull(options);
        Assert.False(options.UseLocalStack);
    }

    [Fact]
    public void UseLocalStackWithNullLocalStackShouldReturnBuilderUnchanged()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig();

        // Act
        var result = builder.UseLocalStack(null);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void UseLocalStackWithLocalStackDisabledShouldReturnBuilderUnchanged()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: false);
        var localStack = builder.AddLocalStack(); // This will return null due to disabled config

        // Act
        var result = builder.UseLocalStack(localStack);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void UseLocalStackWithValidLocalStackShouldReturnBuilder()
    {
        // Arrange
        var builder = CreateBuilderWithLocalStackConfig(useLocalStack: true);
        var localStack = builder.AddLocalStack();

        // Act
        var result = builder.UseLocalStack(localStack);

        // Assert
        Assert.Same(builder, result);
        // The actual configuration logic would be tested in integration tests
        // as it involves complex resource scanning and dependency injection
    }
}
