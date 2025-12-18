namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// Tests for LocalStack Lambda playground resources.
/// These tests verify that CloudFormation stack outputs and AWS resources are created correctly.
/// </summary>
[NotInParallel("IntegrationTests")]
[ClassDataSource<LocalStackLambdaFixture>(Shared = SharedType.PerTestSession)]
public class LocalStackLambdaResourceTests(LocalStackLambdaFixture fixture)
{
    [Test]
    public async Task LocalStack_Should_Be_Healthy(CancellationToken cancellationToken)
    {
        using var httpClient = fixture.App.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cancellationToken);
        var healthContent = await healthResponse.Content.ReadAsStringAsync(cancellationToken);

        await Assert.That(healthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(healthContent).IsNotEmpty();

        await TestOutputHelper.WriteLineAsync($"LocalStack health check passed: {healthResponse.StatusCode}");
        await TestOutputHelper.WriteLineAsync($"Health response: {healthContent}");
    }

    [Test]
    public async Task CloudFormation_Stack_Should_Have_Required_Outputs()
    {
        var qrBucketName = fixture.StackOutputs.GetOutput("QrBucketName");
        var urlsTableName = fixture.StackOutputs.GetOutput("UrlsTableName");
        var analyticsQueueUrl = fixture.StackOutputs.GetOutput("AnalyticsQueueUrl");
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName");

        await Assert.That(qrBucketName).IsNotNull();
        await Assert.That(qrBucketName).IsNotEmpty();
        await Assert.That(urlsTableName).IsNotNull();
        await Assert.That(urlsTableName).IsNotEmpty();
        await Assert.That(analyticsQueueUrl).IsNotNull();
        await Assert.That(analyticsQueueUrl).IsNotEmpty();
        await Assert.That(analyticsTableName).IsNotNull();
        await Assert.That(analyticsTableName).IsNotEmpty();

        await TestOutputHelper.WriteLineAsync($"QrBucketName: {qrBucketName}");
        await TestOutputHelper.WriteLineAsync($"UrlsTableName: {urlsTableName}");
        await TestOutputHelper.WriteLineAsync($"AnalyticsQueueUrl: {analyticsQueueUrl}");
        await TestOutputHelper.WriteLineAsync($"AnalyticsTableName: {analyticsTableName}");
    }

    [Test]
    public async Task S3_QrBucket_Should_Exist(CancellationToken cancellationToken)
    {
        var bucketName = fixture.StackOutputs.GetOutput("QrBucketName")
                         ?? throw new InvalidOperationException("QrBucketName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var s3Client = session.CreateClientByImplementation<AmazonS3Client>();
        var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);

        await Assert.That(doesS3BucketExist).IsTrue().Because($"S3 bucket '{bucketName}' should exist");
        await TestOutputHelper.WriteLineAsync($"S3 bucket '{bucketName}' exists ✓");
    }

    [Test]
    public async Task DynamoDB_UrlsTable_Should_Exist(CancellationToken cancellationToken)
    {
        var tableName = fixture.StackOutputs.GetOutput("UrlsTableName")
                        ?? throw new InvalidOperationException("UrlsTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, cancellationToken);

        await Assert.That(tableResponse).IsNotNull();
        await Assert.That(tableResponse.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(tableResponse.Table.TableName).IsEqualTo(tableName);

        await TestOutputHelper.WriteLineAsync($"DynamoDB table '{tableName}' exists ✓");
        await TestOutputHelper.WriteLineAsync($"  Partition Key: {tableResponse.Table.KeySchema[0].AttributeName}");
    }

    [Test]
    public async Task SQS_AnalyticsQueue_Should_Exist(CancellationToken cancellationToken)
    {
        var queueUrl = fixture.StackOutputs.GetOutput("AnalyticsQueueUrl")
                       ?? throw new InvalidOperationException("AnalyticsQueueUrl output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var sqsClient = session.CreateClientByImplementation<AmazonSQSClient>();
        var queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(queueUrl, ["QueueArn"], cancellationToken);

        await Assert.That(queueAttributesResponse).IsNotNull();
        await Assert.That(queueAttributesResponse.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(queueAttributesResponse.Attributes).IsNotEmpty();
        await Assert.That(queueAttributesResponse.Attributes.ContainsKey("QueueArn")).IsTrue();

        await TestOutputHelper.WriteLineAsync($"SQS queue '{queueUrl}' exists ✓");
        await TestOutputHelper.WriteLineAsync($"  Queue ARN: {queueAttributesResponse.Attributes["QueueArn"]}");
    }

    [Test]
    public async Task DynamoDB_AnalyticsTable_Should_Exist_With_Correct_Schema(CancellationToken cancellationToken)
    {
        var tableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                        ?? throw new InvalidOperationException("AnalyticsTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, cancellationToken);

        await Assert.That(tableResponse).IsNotNull();
        await Assert.That(tableResponse.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(tableResponse.Table.TableName).IsEqualTo(tableName);

        // Verify key schema
        var partitionKey = tableResponse.Table.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
        await Assert.That(partitionKey).IsNotNull();
        await Assert.That(partitionKey.AttributeName).IsEqualTo("EventId");

        var sortKey = tableResponse.Table.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);
        await Assert.That(sortKey).IsNotNull();
        await Assert.That(sortKey.AttributeName).IsEqualTo("Timestamp");

        await TestOutputHelper.WriteLineAsync($"DynamoDB analytics table '{tableName}' exists ✓");
        await TestOutputHelper.WriteLineAsync($"  Partition Key: {partitionKey.AttributeName}");
        await TestOutputHelper.WriteLineAsync($"  Sort Key: {sortKey.AttributeName}");
    }
}
