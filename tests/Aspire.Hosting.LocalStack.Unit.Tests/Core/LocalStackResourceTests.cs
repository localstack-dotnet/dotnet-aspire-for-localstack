namespace Aspire.Hosting.LocalStack.Unit.Tests.Core;

public class LocalStackResourceTests
{
    [Fact]
    public void LocalStackResource_Should_Implement_ILocalStackResource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        Assert.IsType<ILocalStackResource>(resource, exactMatch: false);
        Assert.IsType<IResourceWithWaitSupport>(resource, exactMatch: false);
        Assert.IsType<IResourceWithConnectionString>(resource, exactMatch: false);
    }

    [Fact]
    public void LocalStackResource_Should_Store_Name_And_Options()
    {
        const string resourceName = "my-localstack";
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource(resourceName, options);

        Assert.Equal(resourceName, resource.Name);
        Assert.Same(options, resource.Options);
    }

    [Fact]
    public void LocalStackResource_Should_Generate_HTTP_Connection_String_When_SSL_Disabled()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(edgePort: 4566, useSsl: false);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        Assert.Equal("http://localhost:4566", connectionString);
    }

    [Fact]
    public void LocalStackResource_Should_Generate_HTTPS_Connection_String_When_SSL_Enabled()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(edgePort: 4566, useSsl: true);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        Assert.Equal("https://localhost:4566", connectionString);
    }

    [Fact]
    public void LocalStackResource_Should_Use_Custom_Host_In_Connection_String()
    {
        const string customHost = "custom-localstack-host";
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 4567,
            localStackHost: customHost);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        Assert.Equal("http://custom-localstack-host:4567", connectionString);
    }

    [Fact]
    public void LocalStackResource_Should_Use_Custom_Port_In_Connection_String()
    {
        const int customPort = 9999;
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(edgePort: customPort);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        Assert.Equal($"http://localhost:{customPort}", connectionString);
    }

    [Fact]
    public void LocalStackResource_Should_Have_Primary_Endpoint()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        Assert.NotNull(resource.PrimaryEndpoint);
        Assert.Equal("http", resource.PrimaryEndpoint.EndpointName);
    }

    [Fact]
    public void LocalStackResource_Should_Return_Same_Primary_Endpoint_Instance()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
        var resource = new LocalStackResource("test-localstack", options);

        var endpoint1 = resource.PrimaryEndpoint;
        var endpoint2 = resource.PrimaryEndpoint;

        Assert.Same(endpoint1, endpoint2);
    }

    [Fact]
    public void LocalStackResource_Should_Have_Correct_Primary_Endpoint_Name()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        Assert.Equal(LocalStackResource.PrimaryEndpointName, resource.PrimaryEndpoint.EndpointName);
        Assert.Equal("http", LocalStackResource.PrimaryEndpointName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LocalStackResource_Should_Throw_ArgumentException_For_Invalid_Name(string invalidName)
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        Assert.Throws<ArgumentException>(() => new LocalStackResource(invalidName, options));
    }

    [Fact]
    public void LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Name()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        Assert.Throws<ArgumentNullException>(() => new LocalStackResource(null!, options));
    }

    [Fact]
    public void LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Options()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalStackResource("test", null!));
    }

    [Fact]
    public void LocalStackResource_Should_Be_Container_Resource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        Assert.IsType<ContainerResource>(resource, exactMatch: false);
    }

    [Fact]
    public void LocalStackResource_Connection_String_Should_Handle_Edge_Cases()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
            edgePort: 1,
            localStackHost: "test-host",
            useSsl: true);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        Assert.Equal("https://test-host:1", connectionString);
    }
}
