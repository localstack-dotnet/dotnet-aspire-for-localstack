namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

[Collection("CdkSequential")]
public class LocalStackLambdaHostTests(ITestOutputHelper outputHelper)
{
    [Fact]
#pragma warning disable MA0051 // Method is too long
    public async Task LocalStack_Should_Start_And_Become_Healthy_With_Resources_Created_Async()
#pragma warning restore MA0051
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LocalStack_Lambda_AppHost>(["LocalStack:UseLocalStack=true"], cts.Token);

        // Configure logging to capture test output
        appHost.Services.AddLogging(logging => logging
            .AddXUnit(outputHelper)
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning));

        await using var app = await appHost.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);
        await resourceNotificationService.WaitForResourceAsync("custom", KnownResourceStates.Running, cts.Token);
        await resourceNotificationService.WaitForResourceAsync("CDKBootstrap", KnownResourceStates.Running, cts.Token);

        using var httpClient = app.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var healthContent = await healthResponse.Content.ReadAsStringAsync(cts.Token);

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.NotEmpty(healthContent);

        var resourceChecksSucceeded = false;
        await foreach (var resourceEvent in resourceNotificationService.WatchAsync(cts.Token).ConfigureAwait(false))
        {
            if (!string.Equals("custom", resourceEvent.Resource.Name, StringComparison.Ordinal)
                || resourceEvent.Snapshot.State?.Text is not { Length: > 0 } stateText
                || !string.Equals(stateText, KnownResourceStates.Running, StringComparison.Ordinal))
            {
                continue;
            }

            var bucketName = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.QrBucketName", StringComparison.Ordinal)).Value as string;
            var urlsTableName = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.UrlsTableName", StringComparison.Ordinal)).Value as string;
            var analyticsQueueUrl = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.AnalyticsQueueUrl", StringComparison.Ordinal)).Value as string;
            var analyticsTableName = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.AnalyticsTableName", StringComparison.Ordinal)).Value as string;

            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(urlsTableName) ||
                string.IsNullOrWhiteSpace(analyticsQueueUrl) || string.IsNullOrWhiteSpace(analyticsTableName))
            {
                Assert.Fail("One or more CloudFormation outputs are missing or empty");
                break;
            }

            var connectionString = await app.GetConnectionStringAsync("localstack", cancellationToken: cts.Token);
            Assert.NotNull(connectionString);
            Assert.NotEmpty(connectionString);

            var cloudFormationResource = resourceEvent.Resource as ICloudFormationResource;
            Assert.NotNull(cloudFormationResource);
            var awsSdkConfig = cloudFormationResource.AWSSDKConfig;
            Assert.NotNull(awsSdkConfig);
            Assert.NotNull(awsSdkConfig.Region);

            var connectionStringUri = new Uri(connectionString);

            var configOptions = new ConfigOptions(connectionStringUri.Host, edgePort: connectionStringUri.Port);
            var sessionOptions = new SessionOptions(regionName: awsSdkConfig.Region.SystemName);
            var session = SessionStandalone.Init().WithSessionOptions(sessionOptions).WithConfigurationOptions(configOptions).Create();

            var s3Client = session.CreateClientByImplementation<AmazonS3Client>();
            var sqsClient = session.CreateClientByImplementation<AmazonSQSClient>();
            var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();

            // Verify S3 QR bucket exists
            var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
            Assert.True(doesS3BucketExist, $"S3 bucket '{bucketName}' should exist");

            // Verify URLs table exists
            var urlsTableResponse = await dynamoDbClient.DescribeTableAsync(urlsTableName, cts.Token);
            Assert.NotNull(urlsTableResponse);
            Assert.Equal(HttpStatusCode.OK, urlsTableResponse.HttpStatusCode);

            // Verify analytics SQS queue exists
            var queueAttributesResponse = await sqsClient.GetQueueAttributesAsync(analyticsQueueUrl, ["QueueArn"], cts.Token);
            Assert.NotNull(queueAttributesResponse);
            Assert.Equal(HttpStatusCode.OK, queueAttributesResponse.HttpStatusCode);
            Assert.NotEmpty(queueAttributesResponse.Attributes);

            // Verify analytics DynamoDB table exists
            var analyticsTableResponse = await dynamoDbClient.DescribeTableAsync(analyticsTableName, cts.Token);
            Assert.NotNull(analyticsTableResponse);
            Assert.Equal(HttpStatusCode.OK, analyticsTableResponse.HttpStatusCode);
            Assert.Equal("EventId", analyticsTableResponse.Table.KeySchema[0].AttributeName);

            resourceChecksSucceeded = true;

            break;
        }

        Assert.True(resourceChecksSucceeded);
    }
}
