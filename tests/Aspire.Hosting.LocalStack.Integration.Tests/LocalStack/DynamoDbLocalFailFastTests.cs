namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

/// <summary>
/// Guards the DynamoDB Local fail-fast: UseLocalStack() must reject AddAWSDynamoDBLocal
/// because DynamoDB Local and LocalStack's DynamoDB are competing backends and combining
/// them would split DynamoDB state. Uses the ad-hoc DistributedApplicationTestingBuilder.Create()
/// pattern so the test does not pull in the Lambda playground dependency graph.
/// </summary>
public class DynamoDbLocalFailFastTests
{
    [Test]
    public async Task UseLocalStack_Should_Fail_Fast_When_DynamoDb_Local_Is_Registered(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        // Create has no non-generic async overload; the sync overload is a builder factory (no I/O).
#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create("LocalStack:UseLocalStack=true");
#pragma warning restore CA1849

        var localStack = builder.AddLocalStack("localstack");
        builder.UseLocalStack(localStack);
        builder.AddAWSDynamoDBLocal("dynamodb-local");

        await using var app = await builder.BuildAsync(cts.Token);

        await Assert.That(async () => await app.StartAsync(cts.Token))
            .ThrowsExactly<DistributedApplicationException>();
    }
}
