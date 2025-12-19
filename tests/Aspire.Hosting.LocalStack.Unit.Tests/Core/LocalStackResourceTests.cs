namespace Aspire.Hosting.LocalStack.Unit.Tests.Core;

public class LocalStackResourceTests
{
    [Test]
    public async Task LocalStackResource_Should_Implement_ILocalStackResource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        await Assert.That(resource).IsAssignableTo<ILocalStackResource>();
        await Assert.That(resource).IsAssignableTo<IResourceWithWaitSupport>();
        await Assert.That(resource).IsAssignableTo<IResourceWithConnectionString>();
    }

    [Test]
    public async Task LocalStackResource_Should_Store_Name_And_Options()
    {
        const string resourceName = "my-localstack";
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource(resourceName, options);

        await Assert.That(resource.Name).IsEqualTo(resourceName);
        await Assert.That(resource.Options).IsSameReferenceAs(options);
    }

    [Test]
    public async Task LocalStackResource_Should_Generate_HTTP_Connection_String_When_SSL_Disabled()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(edgePort: 4566, useSsl: false);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        await Assert.That(connectionString).StartsWith("http://");
    }

    [Test]
    public async Task LocalStackResource_Should_Generate_HTTPS_Connection_String_When_SSL_Enabled()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(edgePort: 4566, useSsl: true);

        var resource = new LocalStackResource("test-localstack", options);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        await Assert.That(connectionString).StartsWith("https://");
    }

    [Test]
    public async Task LocalStackResource_Should_Have_Primary_Endpoint()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        await Assert.That(resource.PrimaryEndpoint).IsNotNull();
        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo("http");
    }

    [Test]
    public async Task LocalStackResource_Should_Return_Same_Primary_Endpoint_Instance()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
        var resource = new LocalStackResource("test-localstack", options);

        var endpoint1 = resource.PrimaryEndpoint;
        var endpoint2 = resource.PrimaryEndpoint;

        await Assert.That(endpoint1).IsSameReferenceAs(endpoint2);
    }

    [Test]
    public async Task LocalStackResource_Should_Have_Correct_Primary_Endpoint_Name()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo(LocalStackResource.PrimaryEndpointName);
        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo("http");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task LocalStackResource_Should_Throw_ArgumentException_For_Invalid_Name(string invalidName)
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        await Assert.That(() => new LocalStackResource(invalidName, options)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Name()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        await Assert.That(() => new LocalStackResource(null!, options)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Options()
    {
        await Assert.That(() => new LocalStackResource("test", null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task LocalStackResource_Should_Be_Container_Resource()
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        var resource = new LocalStackResource("test-localstack", options);

        await Assert.That(resource).IsAssignableTo<ContainerResource>();
    }
}
