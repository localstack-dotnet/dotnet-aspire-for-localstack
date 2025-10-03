using Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Provisioning;

/// <summary>
/// Tests for LocalStack CDK provisioning playground resources.
/// These tests verify that CloudFormation stack outputs and AWS resources are created correctly.
/// </summary>
[Collection("LocalStackCDK")]
public class LocalStackCdkResourceTests(LocalStackCdkFixture fixture, ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task LocalStack_Should_Be_Healthy()
    {
        using var httpClient = fixture.App.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var healthContent = await healthResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.NotEmpty(healthContent);

        outputHelper.WriteLine($"LocalStack health check passed: {healthResponse.StatusCode}");
        outputHelper.WriteLine($"Health response: {healthContent}");
    }

    [Fact]
    public void CloudFormation_Stack_Should_Have_Required_Outputs()
    {
        var bucketName = fixture.StackOutputs.GetOutput("BucketName");
        var chatTopicArn = fixture.StackOutputs.GetOutput("ChatTopicArn");
        var chatMessagesQueueUrl = fixture.StackOutputs.GetOutput("ChatMessagesQueueUrl");
        var chatMessagesTableName = fixture.StackOutputs.GetOutput("ChatMessagesTableName");

        Assert.NotNull(bucketName);
        Assert.NotEmpty(bucketName);
        Assert.NotNull(chatTopicArn);
        Assert.NotEmpty(chatTopicArn);
        Assert.NotNull(chatMessagesQueueUrl);
        Assert.NotEmpty(chatMessagesQueueUrl);
        Assert.NotNull(chatMessagesTableName);
        Assert.NotEmpty(chatMessagesTableName);

        outputHelper.WriteLine($"BucketName: {bucketName}");
        outputHelper.WriteLine($"ChatTopicArn: {chatTopicArn}");
        outputHelper.WriteLine($"ChatMessagesQueueUrl: {chatMessagesQueueUrl}");
        outputHelper.WriteLine($"ChatMessagesTableName: {chatMessagesTableName}");
    }

    [Fact]
    public async Task S3_Bucket_Should_Exist()
    {
        var bucketName = fixture.StackOutputs.GetOutput("BucketName")
            ?? throw new InvalidOperationException("BucketName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var s3Client = session.CreateClientByImplementation<AmazonS3Client>();
        var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);

        Assert.True(doesS3BucketExist, $"S3 bucket '{bucketName}' should exist");
        outputHelper.WriteLine($"S3 bucket '{bucketName}' exists ✓");
    }

    [Fact]
    public async Task SNS_ChatTopic_Should_Exist()
    {
        var topicArn = fixture.StackOutputs.GetOutput("ChatTopicArn")
            ?? throw new InvalidOperationException("ChatTopicArn output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var snsClient = session.CreateClientByImplementation<AmazonSimpleNotificationServiceClient>();
        var topicAttributesResponse = await snsClient.GetTopicAttributesAsync(topicArn, TestContext.Current.CancellationToken);

        Assert.NotNull(topicAttributesResponse);
        Assert.Equal(HttpStatusCode.OK, topicAttributesResponse.HttpStatusCode);
        Assert.NotEmpty(topicAttributesResponse.Attributes);

        outputHelper.WriteLine($"SNS topic '{topicArn}' exists ✓");
        outputHelper.WriteLine($"  Display Name: {topicAttributesResponse.Attributes.GetValueOrDefault("DisplayName", "N/A")}");
    }

    [Fact]
    public async Task SQS_ChatMessagesQueue_Should_Exist()
    {
        var queueUrl = fixture.StackOutputs.GetOutput("ChatMessagesQueueUrl")
            ?? throw new InvalidOperationException("ChatMessagesQueueUrl output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var sqsClient = session.CreateClientByImplementation<AmazonSQSClient>();
        var queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(queueUrl, ["QueueArn"], TestContext.Current.CancellationToken);

        Assert.NotNull(queueAttributesResponse);
        Assert.Equal(HttpStatusCode.OK, queueAttributesResponse.HttpStatusCode);
        Assert.NotEmpty(queueAttributesResponse.Attributes);
        Assert.True(queueAttributesResponse.Attributes.ContainsKey("QueueArn"));

        outputHelper.WriteLine($"SQS queue '{queueUrl}' exists ✓");
        outputHelper.WriteLine($"  Queue ARN: {queueAttributesResponse.Attributes["QueueArn"]}");
    }

    [Fact]
    public async Task DynamoDB_ChatMessagesTable_Should_Exist()
    {
        var tableName = fixture.StackOutputs.GetOutput("ChatMessagesTableName")
            ?? throw new InvalidOperationException("ChatMessagesTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, TestContext.Current.CancellationToken);

        Assert.NotNull(tableResponse);
        Assert.Equal(HttpStatusCode.OK, tableResponse.HttpStatusCode);
        Assert.Equal(tableName, tableResponse.Table.TableName);

        outputHelper.WriteLine($"DynamoDB table '{tableName}' exists ✓");
        outputHelper.WriteLine($"  Partition Key: {tableResponse.Table.KeySchema[0].AttributeName}");
    }
}
