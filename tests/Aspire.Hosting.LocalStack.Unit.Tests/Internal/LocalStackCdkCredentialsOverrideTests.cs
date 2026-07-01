using Amazon;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

[NotInParallel("AwsSdkCredentialsGenerators")]
public class LocalStackCdkCredentialsOverrideTests
{
    [Test]
    public async Task Apply_Should_Install_Generator_Returning_LocalStack_Session_Credentials()
    {
        var previous = AWSConfigs.AWSCredentialsGenerators;
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();

        try
        {
            LocalStackCdkCredentialsOverride.Apply(options);

            var generators = AWSConfigs.AWSCredentialsGenerators;
            await Assert.That(generators).IsNotNull();
            await Assert.That(generators!).HasSingleItem();

            var credentials = generators[0]();
            var immutable = await credentials.GetCredentialsAsync();
            await Assert.That(immutable.AccessKey).IsEqualTo(options.Session.AwsAccessKeyId);
            await Assert.That(immutable.SecretKey).IsEqualTo(options.Session.AwsAccessKey);
            await Assert.That(immutable.Token).IsEqualTo(options.Session.AwsSessionToken);
        }
        finally
        {
            AWSConfigs.AWSCredentialsGenerators = previous;
        }
    }
}
