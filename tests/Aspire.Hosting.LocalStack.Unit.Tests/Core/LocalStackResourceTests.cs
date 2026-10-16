namespace Aspire.Hosting.LocalStack.Unit.Tests.Core;

public class LocalStackResourceTests
{
    [Test]
    public async Task LocalStackResource_Should_Implement_ILocalStackResource()
    {
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource("test-localstack", state);

        await Assert.That(resource).IsAssignableTo<ILocalStackResource>();
        await Assert.That(resource).IsAssignableTo<IResourceWithWaitSupport>();
        await Assert.That(resource).IsAssignableTo<IResourceWithConnectionString>();
    }

    [Test]
    public async Task LocalStackResource_Should_Store_Name_And_Adapted_Options()
    {
        const string resourceName = "my-localstack";
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource(resourceName, state);

        await Assert.That(resource.Name).IsEqualTo(resourceName);
        await Assert.That(resource.GetHostingState()).IsSameReferenceAs(state);
    }

    [Test]
    public async Task LocalStackResource_Should_Generate_HTTP_Connection_String_When_SSL_Disabled()
    {
        var state = TestDataBuilders.CreateHostingState(useSsl: false);

        var resource = new LocalStackResource("test-localstack", state);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        await Assert.That(connectionString).StartsWith("http://");
    }

    [Test]
    public async Task LocalStackResource_Should_Generate_HTTPS_Connection_String_When_SSL_Enabled()
    {
        var state = TestDataBuilders.CreateHostingState(useSsl: true);

        var resource = new LocalStackResource("test-localstack", state);

        var connectionString = resource.ConnectionStringExpression.ValueExpression;
        await Assert.That(connectionString).StartsWith("https://");
    }

    [Test]
    public async Task LocalStackResource_Should_Have_Primary_Endpoint()
    {
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource("test-localstack", state);

        await Assert.That(resource.PrimaryEndpoint).IsNotNull();
        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo("http");
    }

    [Test]
    public async Task LocalStackResource_Should_Return_Same_Primary_Endpoint_Instance()
    {
        var state = TestDataBuilders.CreateHostingState();
        var resource = new LocalStackResource("test-localstack", state);

        var endpoint1 = resource.PrimaryEndpoint;
        var endpoint2 = resource.PrimaryEndpoint;

        await Assert.That(endpoint1).IsSameReferenceAs(endpoint2);
    }

    [Test]
    public async Task LocalStackResource_Should_Have_Correct_Primary_Endpoint_Name()
    {
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource("test-localstack", state);

        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo(LocalStackResource.PrimaryEndpointName);
        await Assert.That(resource.PrimaryEndpoint.EndpointName).IsEqualTo("http");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task LocalStackResource_Should_Throw_ArgumentException_For_Invalid_Name(string invalidName)
    {
        var state = TestDataBuilders.CreateHostingState();

        await Assert.That(() => new LocalStackResource(invalidName, state)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Name()
    {
        var state = TestDataBuilders.CreateHostingState();

        await Assert.That(() => new LocalStackResource(null!, state)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task LocalStackResource_Should_Throw_ArgumentNullException_For_Null_Options()
    {
#pragma warning disable CS0618
        await Assert.That(() => new LocalStackResource("test", (ILocalStackOptions)null!)).ThrowsExactly<ArgumentNullException>();
#pragma warning restore CS0618
    }

    [Test]
    public async Task LocalStackResource_Should_Be_Container_Resource()
    {
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource("test-localstack", state);

        await Assert.That(resource).IsAssignableTo<ContainerResource>();
    }
}
