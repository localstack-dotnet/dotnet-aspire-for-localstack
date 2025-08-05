namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Provisioning;

public class LocalStackCDKHostTests
{
    [Fact]
#pragma warning disable MA0051
    public async Task LocalStack_Should_Start_And_Become_Healthy_With_Resources_Created_Async()
#pragma warning restore MA0051
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LocalStack_Provisioning_CDK_AppHost>(["LocalStack:UseLocalStack=true"], cts.Token);
        await using var app = await appHost.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);
        await resourceNotificationService.WaitForResourceAsync("custom", KnownResourceStates.Running, cts.Token);
        await resourceNotificationService.WaitForResourceAsync("CDKBootstrap", KnownResourceStates.Running, cts.Token);
        // await resourceNotificationService.WaitForResourceHealthyAsync("Frontend", cts.Token);

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
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.BucketName", StringComparison.Ordinal)).Value as string;
            var chatTopicArn = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.ChatTopicArn", StringComparison.Ordinal)).Value as string;
            var chatMessagesQueueUrl = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.ChatMessagesQueueUrl", StringComparison.Ordinal)).Value as string;
            var chatMessagesTableName = resourceEvent.Snapshot.Properties
                .Single(snapshot => string.Equals(snapshot.Name, "aws.cloudformation.output.ChatMessagesTableName", StringComparison.Ordinal)).Value as string;

            if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(chatTopicArn) || string.IsNullOrWhiteSpace(chatMessagesQueueUrl) ||
                string.IsNullOrWhiteSpace(chatMessagesTableName))
            {
                Assert.Fail();
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
            var snsClient = session.CreateClientByImplementation<AmazonSimpleNotificationServiceClient>();
            var dynamoDbClient = session.CreateClientByImplementation<AmazonDynamoDBClient>();

            var doesS3BucketExist = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucketName);
            Assert.True(doesS3BucketExist);

            var getQueueAttributesResponse = await sqsClient.GetQueueAttributesAsync(chatMessagesQueueUrl, ["QueueArn"], cts.Token);
            Assert.NotNull(getQueueAttributesResponse);
            Assert.Equal(HttpStatusCode.OK, getQueueAttributesResponse.HttpStatusCode);

            var getTopicAttributesResponse = await snsClient.GetTopicAttributesAsync(chatTopicArn, cts.Token);
            Assert.NotNull(getTopicAttributesResponse);
            Assert.Equal(HttpStatusCode.OK, getTopicAttributesResponse.HttpStatusCode);

            var describeTableResponse = await dynamoDbClient.DescribeTableAsync(chatMessagesTableName, cts.Token);
            Assert.NotNull(describeTableResponse);
            Assert.Equal(HttpStatusCode.OK, describeTableResponse.HttpStatusCode);

            resourceChecksSucceeded = true;

            break;
        }

        Assert.True(resourceChecksSucceeded);

//         var stackResource = (IStackResource)appHost.Resources.Single(resource => string.Equals(resource.Name, "custom", StringComparison.Ordinal));
// #pragma warning disable S1481
//         var stackResourceStack = (CustomStack)stackResource.Stack;
// #pragma warning restore S1481

        // using var frontendClient = app.CreateHttpClient("Frontend");
        //
        // var dynamoHealthCheckResult = await frontendClient.GetStringAsync(new Uri("/healthcheck/dynamodb", UriKind.Relative), cts.Token);
        // var cfHealthCheckResult = await frontendClient.GetStringAsync(new Uri("/healthcheck/cloudformation", UriKind.Relative), cts.Token);
        //
        // Assert.Contains("Healthy", dynamoHealthCheckResult, StringComparison.Ordinal);
        // Assert.Equal("\"Success\"", cfHealthCheckResult);
    }
}
