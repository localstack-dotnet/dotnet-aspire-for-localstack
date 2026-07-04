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
        // Injecting DynamoDB Local after that pass guards the final app model, not only
        // UseLocalStack()'s call-time scan.
        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LocalStack_Lambda_AppHost>(["LocalStack:UseLocalStack=true"], cts.Token);

        appHost.AddAWSDynamoDBLocal("dynamodb-local");

        await using var app = await appHost.BuildAsync(cts.Token);

        await Assert.That(async () => await app.StartAsync(cts.Token)).ThrowsExactly<DistributedApplicationException>();
    }
}
