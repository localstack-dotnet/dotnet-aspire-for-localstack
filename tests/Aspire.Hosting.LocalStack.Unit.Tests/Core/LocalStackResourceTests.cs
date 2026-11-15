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
    public async Task Legacy_Constructor_Should_Return_The_Supplied_Options_Unchanged()
    {
        var options = new LocalStackOptions(
            true,
            new SessionOptions("key", "secret", "token", "eu-west-1"),
            new ConfigOptions("custom-host", useSsl: false, useLegacyPorts: false, edgePort: 4599));

#pragma warning disable CS0618
        var resource = new LocalStackResource("test", options);

        await Assert.That(resource.Options).IsSameReferenceAs(options);
        await Assert.That(resource.Options.Config.LocalStackHost).IsEqualTo("custom-host");
        await Assert.That(resource.Options.Config.EdgePort).IsEqualTo(4599);
#pragma warning restore CS0618
    }

    [Test]
    public async Task Hosting_State_Of_An_External_Resource_Is_Read_From_Its_Options()
    {
        var resource = new ExternalLocalStackResource("external", new LocalStackOptions(
            true,
            new SessionOptions("key", "secret", "token", "eu-west-1"),
            new ConfigOptions(useSsl: true, useLegacyPorts: true)));

        var state = resource.GetHostingState();

        await Assert.That(state).IsEqualTo(new LocalStackHostingState
        {
            Enabled = true,
            Region = "eu-west-1",
            AccessKeyId = "key",
            SecretAccessKey = "secret",
            SessionToken = "token",
            UseSsl = true,
            UseLegacyPorts = true,
        });
    }

    [Test]
    public async Task UseLocalStack_Accepts_A_Disabled_External_Resource()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var resource = builder.AddResource<ILocalStackResource>(
            new ExternalLocalStackResource("external", new LocalStackOptions(false, new SessionOptions(), new ConfigOptions())));

        await Assert.That(builder.UseLocalStack(resource)).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task LocalStackResource_Should_Be_Container_Resource()
    {
        var state = TestDataBuilders.CreateHostingState();

        var resource = new LocalStackResource("test-localstack", state);

        await Assert.That(resource).IsAssignableTo<ContainerResource>();
    }

    /// <summary>
    /// An <see cref="ILocalStackResource"/> implemented outside the package, as a 13.4.0 consumer could write it.
    /// </summary>
    private sealed class ExternalLocalStackResource(string name, ILocalStackOptions options) : ILocalStackResource
    {
        public string Name { get; } = name;

        public ResourceAnnotationCollection Annotations { get; } = [];

        public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Empty;

#pragma warning disable CS0618
        public ILocalStackOptions Options { get; } = options;
#pragma warning restore CS0618
    }
}
