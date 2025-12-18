namespace Aspire.Hosting.LocalStack.Integration.Tests.EagerLoadedServices;

[NotInParallel("IntegrationTests")]
public class EagerLoadedServicesTests
{
    [Test]
    public async Task LocalStack_Should_Lazy_Load_Services_By_Default_Async(CancellationToken cancellationToken)
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, cancellationToken);

#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create("LocalStack:UseLocalStack=true");
#pragma warning restore CA1849

        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);
        builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
        {
            container.Lifetime = ContainerLifetime.Session;
            container.DebugLevel = 1;
            container.LogLevel = LocalStackLogLevel.Debug;
        });

        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);

        using var httpClient = app.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var healthContent = await healthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        await Assert.That(healthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var servicesNode = healthContent?["services"]?.AsObject();
        await Assert.That(servicesNode).IsNotNull();
        await Assert.That(servicesNode.ContainsKey("sqs")).IsTrue();
        await Assert.That(servicesNode["sqs"]?.ToString()).IsNotEqualTo("running");

        var connectionString = await app.GetConnectionStringAsync("localstack", cancellationToken: cts.Token);
        await Assert.That(connectionString).IsNotNull();
        await Assert.That(connectionString).IsNotEmpty();

        var connectionStringUri = new Uri(connectionString);

        var configOptions = new ConfigOptions(connectionStringUri.Host, edgePort: connectionStringUri.Port);
        var sessionOptions = new SessionOptions(regionName: awsConfig.Region!.SystemName);
        var session = SessionStandalone.Init().WithSessionOptions(sessionOptions).WithConfigurationOptions(configOptions).Create();

        var sqsClient = session.CreateClientByImplementation<AmazonSQSClient>();
        await sqsClient.ListQueuesAsync(new ListQueuesRequest(), cts.Token);

        var laterHealthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var laterHealthContent = await laterHealthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        await Assert.That(laterHealthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var sqsServicesNode = laterHealthContent?["services"]?.AsObject();
        await Assert.That(sqsServicesNode).IsNotNull();
        await Assert.That(sqsServicesNode.ContainsKey("sqs")).IsTrue();
        await Assert.That(sqsServicesNode["sqs"]?.ToString()).IsEqualTo("running");
    }

    [Test]
    public async Task LocalStack_Should_Eagerly_Load_Services_When_Configured_Async(CancellationToken cancellationToken)
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, cancellationToken);

#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create("LocalStack:UseLocalStack=true");
#pragma warning restore CA1849

        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);
        builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
        {
            container.Lifetime = ContainerLifetime.Session;
            container.DebugLevel = 1;
            container.LogLevel = LocalStackLogLevel.Debug;
            container.EagerLoadedServices = [AwsService.Sqs];
        });

        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();

        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);

        using var httpClient = app.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var healthContent = await healthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        await Assert.That(healthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var servicesNode = healthContent?["services"]?.AsObject();
        await Assert.That(servicesNode).IsNotNull();
        await Assert.That(servicesNode.ContainsKey("sqs")).IsTrue();
        await Assert.That(servicesNode["sqs"]?.ToString()).IsEqualTo("running");
    }

    [Test]
    public async Task LocalStack_Should_Eagerly_Load_Multiple_Services_Async(CancellationToken cancellationToken)
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, cancellationToken);

#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create("LocalStack:UseLocalStack=true");
#pragma warning restore CA1849

        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);
        builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
        {
            container.Lifetime = ContainerLifetime.Session;
            container.DebugLevel = 1;
            container.LogLevel = LocalStackLogLevel.Debug;
            container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3];
        });

        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);

        using var httpClient = app.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var healthContent = await healthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        await Assert.That(healthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var servicesNode = healthContent?["services"]?.AsObject();
        await Assert.That(servicesNode).IsNotNull();

        // All three services should be running
        await Assert.That(servicesNode.ContainsKey("sqs")).IsTrue();
        await Assert.That(servicesNode["sqs"]?.ToString()).IsEqualTo("running");
        await Assert.That(servicesNode.ContainsKey("dynamodb")).IsTrue();
        await Assert.That(servicesNode["dynamodb"]?.ToString()).IsEqualTo("running");
        await Assert.That(servicesNode.ContainsKey("s3")).IsTrue();
        await Assert.That(servicesNode["s3"]?.ToString()).IsEqualTo("running");
    }

    [Test]
    public async Task LocalStack_Should_Handle_Empty_EagerLoadedServices_Like_Lazy_Loading_Async(CancellationToken cancellationToken)
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, cancellationToken);

#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create("LocalStack:UseLocalStack=true");
#pragma warning restore CA1849

        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);
        builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
        {
            container.Lifetime = ContainerLifetime.Session;
            container.DebugLevel = 1;
            container.LogLevel = LocalStackLogLevel.Debug;
            container.EagerLoadedServices = []; // Explicitly empty
        });

        await using var app = await builder.BuildAsync(cts.Token);
        await app.StartAsync(cts.Token);

        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);

        using var httpClient = app.CreateHttpClient("localstack", "http");
        var healthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var healthContent = await healthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        await Assert.That(healthResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var servicesNode = healthContent?["services"]?.AsObject();
        await Assert.That(servicesNode).IsNotNull();

        // Services should not be running by default (lazy loading)
        var sqsService = servicesNode["sqs"];
        if (sqsService is not null)
        {
            await Assert.That(sqsService.ToString()).IsNotEqualTo("running");
        }
    }

}
