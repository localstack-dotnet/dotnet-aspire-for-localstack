using Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// Tests for LocalStack Lambda playground resources.
/// These tests verify that CloudFormation stack outputs and AWS resources are created correctly.
/// </summary>
[Collection("LocalStackLambda")]
public class LocalStackLambdaResourceTests(LocalStackLambdaFixture fixture, ITestOutputHelper outputHelper)
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
        var qrBucketName = fixture.StackOutputs.GetOutput("QrBucketName");
        var urlsTableName = fixture.StackOutputs.GetOutput("UrlsTableName");
        var analyticsQueueUrl = fixture.StackOutputs.GetOutput("AnalyticsQueueUrl");
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName");

        Assert.NotNull(qrBucketName);
        Assert.NotEmpty(qrBucketName);
        Assert.NotNull(urlsTableName);
        Assert.NotEmpty(urlsTableName);
        Assert.NotNull(analyticsQueueUrl);
        Assert.NotEmpty(analyticsQueueUrl);
        Assert.NotNull(analyticsTableName);
        Assert.NotEmpty(analyticsTableName);

        outputHelper.WriteLine($"QrBucketName: {qrBucketName}");
        outputHelper.WriteLine($"UrlsTableName: {urlsTableName}");
        outputHelper.WriteLine($"AnalyticsQueueUrl: {analyticsQueueUrl}");
        outputHelper.WriteLine($"AnalyticsTableName: {analyticsTableName}");
    }

    [Fact]
    public async Task S3_QrBucket_Should_Exist()
    {
        var bucketName = fixture.StackOutputs.GetOutput("QrBucketName")
                         ?? throw new InvalidOperationException("QrBucketName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var s3Client = session.CreateClientByImplementation<AmazonS3Client>();
        var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);

        Assert.True(doesS3BucketExist, $"S3 bucket '{bucketName}' should exist");
        outputHelper.WriteLine($"S3 bucket '{bucketName}' exists ✓");
    }

    [Fact]
    public async Task DynamoDB_UrlsTable_Should_Exist()
    {
        var tableName = fixture.StackOutputs.GetOutput("UrlsTableName")
                        ?? throw new InvalidOperationException("UrlsTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, TestContext.Current.CancellationToken);

        Assert.NotNull(tableResponse);
        Assert.Equal(HttpStatusCode.OK, tableResponse.HttpStatusCode);
        Assert.Equal(tableName, tableResponse.Table.TableName);

        outputHelper.WriteLine($"DynamoDB table '{tableName}' exists ✓");
        outputHelper.WriteLine($"  Partition Key: {tableResponse.Table.KeySchema[0].AttributeName}");
    }

    [Fact]
    public async Task SQS_AnalyticsQueue_Should_Exist()
    {
        var queueUrl = fixture.StackOutputs.GetOutput("AnalyticsQueueUrl")
                       ?? throw new InvalidOperationException("AnalyticsQueueUrl output not found");

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
    public async Task DynamoDB_AnalyticsTable_Should_Exist_With_Correct_Schema()
    {
        var tableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                        ?? throw new InvalidOperationException("AnalyticsTableName output not found");

        var session = LocalStackTestHelpers.CreateLocalStackSession(fixture.LocalStackConnectionString, fixture.RegionName);

        var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();
        var tableResponse = await dynamoDbClient.DescribeTableAsync(tableName, TestContext.Current.CancellationToken);

        Assert.NotNull(tableResponse);
        Assert.Equal(HttpStatusCode.OK, tableResponse.HttpStatusCode);
        Assert.Equal(tableName, tableResponse.Table.TableName);

        // Verify key schema
        var partitionKey = tableResponse.Table.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.HASH);
        Assert.NotNull(partitionKey);
        Assert.Equal("EventId", partitionKey.AttributeName);

        var sortKey = tableResponse.Table.KeySchema.FirstOrDefault(k => k.KeyType == KeyType.RANGE);
        Assert.NotNull(sortKey);
        Assert.Equal("Timestamp", sortKey.AttributeName);

        outputHelper.WriteLine($"DynamoDB analytics table '{tableName}' exists ✓");
        outputHelper.WriteLine($"  Partition Key: {partitionKey.AttributeName}");
        outputHelper.WriteLine($"  Sort Key: {sortKey.AttributeName}");
    }
}
