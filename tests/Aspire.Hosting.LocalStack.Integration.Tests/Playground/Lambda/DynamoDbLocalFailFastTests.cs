namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// Guards the DynamoDB Local fail-fast against the real Lambda playground app model.
/// UseLocalStack() must reject AddAWSDynamoDBLocal because DynamoDB Local and LocalStack's
/// DynamoDB are competing backends and combining them would split DynamoDB state.
/// </summary>
public class DynamoDbLocalFailFastTests
{
    [Test]
    public async Task UseLocalStack_Should_Fail_Fast_When_DynamoDb_Local_Joins_The_Playground_Model(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        // The playground's Program.cs has already executed by the time CreateAsync returns,
        // including a successful UseLocalStack() pass over a model without DynamoDB Local.
        // The app is intentionally never built or started: the guard fires during model
        // composition, so this test observes it by injecting DynamoDB Local into the real
        // playground graph and invoking UseLocalStack() again.
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LocalStack_Lambda_AppHost>(["LocalStack:UseLocalStack=true"], cts.Token);

        var localStackResource = appHost.Resources.OfType<ILocalStackResource>().Single();
        var localStack = appHost.CreateResourceBuilder(localStackResource);

        appHost.AddAWSDynamoDBLocal("dynamodb-local");

        await Assert.That(() => appHost.UseLocalStack(localStack)).ThrowsExactly<DistributedApplicationException>();
    }
}
