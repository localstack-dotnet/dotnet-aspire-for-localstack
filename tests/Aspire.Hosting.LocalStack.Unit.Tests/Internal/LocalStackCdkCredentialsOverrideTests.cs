using Amazon;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

[NotInParallel("AwsSdkCredentialsGenerators")]
public class LocalStackCdkCredentialsOverrideTests
{
    [Test]
    public async Task Apply_Should_Install_Generator_Returning_LocalStack_Session_Credentials()
    {
        var previous = AWSConfigs.AWSCredentialsGenerators;
        var state = TestDataBuilders.CreateHostingState();

        try
        {
            LocalStackCdkCredentialsOverride.Apply(state);

            var generators = AWSConfigs.AWSCredentialsGenerators;
            await Assert.That(generators).IsNotNull();
            await Assert.That(generators!).HasSingleItem();

            var credentials = generators[0]();
            var immutable = await credentials.GetCredentialsAsync();
            await Assert.That(immutable.AccessKey).IsEqualTo(state.AccessKeyId);
            await Assert.That(immutable.SecretKey).IsEqualTo(state.SecretAccessKey);
            await Assert.That(immutable.Token).IsEqualTo(state.SessionToken);
        }
        finally
        {
            AWSConfigs.AWSCredentialsGenerators = previous;
        }
    }
}
