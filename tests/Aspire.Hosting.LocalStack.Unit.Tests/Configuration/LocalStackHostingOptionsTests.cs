namespace Aspire.Hosting.LocalStack.Unit.Tests.Configuration;

public class LocalStackHostingOptionsTests
{
    [Test]
    public async Task Defaults_Should_Preserve_LocalStackClient_Defaults()
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(options.Enabled).IsFalse();
        await Assert.That(options.Region).IsEqualTo("us-east-1");
        await Assert.That(options.AccessKeyId).IsEqualTo("accessKey");
        await Assert.That(options.SecretAccessKey).IsEqualTo("secretKey");
        await Assert.That(options.SessionToken).IsEqualTo("token");
    }

    [Test]
    public async Task Fluent_Methods_Should_Mutate_And_Return_The_Same_Instance()
    {
        var options = new LocalStackHostingOptions();

        var result = options
            .WithEnabled(true)
            .WithRegion("eu-central-1")
            .WithCredentials("id", "secret", "token-value");

        await Assert.That(result).IsSameReferenceAs(options);
        await Assert.That(options.Enabled).IsTrue();
        await Assert.That(options.Region).IsEqualTo("eu-central-1");
        await Assert.That(options.AccessKeyId).IsEqualTo("id");
        await Assert.That(options.SecretAccessKey).IsEqualTo("secret");
        await Assert.That(options.SessionToken).IsEqualTo("token-value");
    }

    [Test]
    public async Task WithEnabled_NullOptions_ThrowsArgumentNullException()
    {
        LocalStackHostingOptions? options = null;

        await Assert.That(() => options!.WithEnabled(true))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithRegion_NullOptions_ThrowsArgumentNullException()
    {
        LocalStackHostingOptions? options = null;

        await Assert.That(() => options!.WithRegion("us-east-1"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithRegion_NullRegion_ThrowsArgumentNullException()
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithRegion(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("\t")]
    public async Task WithRegion_InvalidRegion_ThrowsArgumentException(string region)
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithRegion(region))
            .ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task WithCredentials_NullOptions_ThrowsArgumentNullException()
    {
        LocalStackHostingOptions? options = null;

        await Assert.That(() => options!.WithCredentials("id", "secret", "token"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithCredentials_NullAccessKeyId_ThrowsArgumentNullException()
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithCredentials(null!, "secret", "token"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithCredentials_NullSecretAccessKey_ThrowsArgumentNullException()
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithCredentials("id", null!, "token"))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithCredentials_NullSessionToken_ThrowsArgumentNullException()
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithCredentials("id", "secret", null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    [Arguments("", "secret", "token")]
    [Arguments(" ", "secret", "token")]
    [Arguments("id", "", "token")]
    [Arguments("id", " ", "token")]
    [Arguments("id", "secret", "")]
    [Arguments("id", "secret", " ")]
    public async Task WithCredentials_InvalidArgument_ThrowsArgumentException(
        string accessKeyId,
        string secretAccessKey,
        string sessionToken)
    {
        var options = new LocalStackHostingOptions();

        await Assert.That(() => options.WithCredentials(accessKeyId, secretAccessKey, sessionToken))
            .ThrowsExactly<ArgumentException>();
    }
}
