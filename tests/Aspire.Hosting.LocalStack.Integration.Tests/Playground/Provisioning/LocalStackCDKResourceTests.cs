namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Provisioning;

/// <summary>
/// Tests for LocalStack CDK provisioning playground resources.
/// These tests verify that CloudFormation stack outputs and AWS resources are created correctly.
/// </summary>
[NotInParallel("IntegrationTests")]
[ClassDataSource<LocalStackCdkFixture>(Shared = SharedType.PerTestSession)]
public class LocalStackCdkResourceTests(LocalStackCdkFixture fixture)
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
        var bucketName = fixture.StackOutputs.GetOutput("BucketName");
        var chatTopicArn = fixture.StackOutputs.GetOutput("ChatTopicArn");
        var chatMessagesQueueUrl = fixture.StackOutputs.GetOutput("ChatMessagesQueueUrl");
        var chatMessagesTableName = fixture.StackOutputs.GetOutput("ChatMessagesTableName");

        await Assert.That(bucketName).IsNotNull();
        await Assert.That(bucketName).IsNotEmpty();
        await Assert.That(chatTopicArn).IsNotNull();
        await Assert.That(chatTopicArn).IsNotEmpty();
        await Assert.That(chatMessagesQueueUrl).IsNotNull();
        await Assert.That(chatMessagesQueueUrl).IsNotEmpty();
        await Assert.That(chatMessagesTableName).IsNotNull();
        await Assert.That(chatMessagesTableName).IsNotEmpty();

        await TestOutputHelper.WriteLineAsync($"BucketName: {bucketName}");
        await TestOutputHelper.WriteLineAsync($"ChatTopicArn: {chatTopicArn}");
        await TestOutputHelper.WriteLineAsync($"ChatMessagesQueueUrl: {chatMessagesQueueUrl}");
        await TestOutputHelper.WriteLineAsync($"ChatMessagesTableName: {chatMessagesTableName}");
    }

    [Test]
    public async Task S3_Bucket_Should_Exist(CancellationToken cancellationToken)
    {
        var bucketName = fixture.StackOutputs.GetOutput("BucketName")
            ?? throw new InvalidOperationException("BucketName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var s3Client = session.CreateClientByImplementation<AmazonS3Client>();
        var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);

        await Assert.That(doesS3BucketExist).IsTrue().Because($"S3 bucket '{bucketName}' should exist");
        await TestOutputHelper.WriteLineAsync($"S3 bucket '{bucketName}' exists ✓");
    }

    [Test]
    public async Task SNS_ChatTopic_Should_Exist(CancellationToken cancellationToken)
    {
        var topicArn = fixture.StackOutputs.GetOutput("ChatTopicArn")
            ?? throw new InvalidOperationException("ChatTopicArn output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var snsClient = session.CreateClientByImplementation<AmazonSimpleNotificationServiceClient>();
        var topicAttributesResponse = await snsClient.GetTopicAttributesAsync(topicArn, cancellationToken);

        await Assert.That(topicAttributesResponse).IsNotNull();
        await Assert.That(topicAttributesResponse.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(topicAttributesResponse.Attributes).IsNotEmpty();

        await TestOutputHelper.WriteLineAsync($"SNS topic '{topicArn}' exists ✓");
        await TestOutputHelper.WriteLineAsync($"  Display Name: {topicAttributesResponse.Attributes.GetValueOrDefault("DisplayName", "N/A")}");
    }

    [Test]
    public async Task SQS_ChatMessagesQueue_Should_Exist(CancellationToken cancellationToken)
    {
        var queueUrl = fixture.StackOutputs.GetOutput("ChatMessagesQueueUrl")
            ?? throw new InvalidOperationException("ChatMessagesQueueUrl output not found");

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
    public async Task DynamoDB_ChatMessagesTable_Should_Exist(CancellationToken cancellationToken)
    {
        var tableName = fixture.StackOutputs.GetOutput("ChatMessagesTableName")
            ?? throw new InvalidOperationException("ChatMessagesTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, cancellationToken);

        await Assert.That(tableResponse).IsNotNull();
        await Assert.That(tableResponse.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(tableResponse.Table.TableName).IsEqualTo(tableName);

        await TestOutputHelper.WriteLineAsync($"DynamoDB table '{tableName}' exists ✓");
        await TestOutputHelper.WriteLineAsync($"  Partition Key: {tableResponse.Table.KeySchema[0].AttributeName}");
    }
}
