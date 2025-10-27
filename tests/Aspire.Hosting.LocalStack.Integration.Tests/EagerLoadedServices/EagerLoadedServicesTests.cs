using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Amazon;
using Amazon.SQS.Model;
using Aspire.Hosting.LocalStack.Container;
using LocalStack.Client.Enums;

namespace Aspire.Hosting.LocalStack.Integration.Tests.EagerLoadedServices;

public class EagerLoadedServicesTests
{
    [Fact]
    public async Task LocalStack_Should_Lazy_Load_Services_By_Default_Async()
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

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
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var servicesNode = healthContent?["services"]?.AsObject();
        Assert.NotNull(servicesNode);
        Assert.True(servicesNode.ContainsKey("sqs"));
        Assert.NotEqual("running", servicesNode["sqs"]?.ToString());

        var connectionString = await app.GetConnectionStringAsync("localstack", cancellationToken: cts.Token);
        Assert.NotNull(connectionString);
        Assert.NotEmpty(connectionString);

        var connectionStringUri = new Uri(connectionString);

        var configOptions = new ConfigOptions(connectionStringUri.Host, edgePort: connectionStringUri.Port);
        var sessionOptions = new SessionOptions(regionName: awsConfig.Region!.SystemName);
        var session = SessionStandalone.Init().WithSessionOptions(sessionOptions).WithConfigurationOptions(configOptions).Create();

        var sqsClient = session.CreateClientByImplementation<AmazonSQSClient>();
        await sqsClient.ListQueuesAsync(new ListQueuesRequest(), cts.Token);

        var laterHealthResponse = await httpClient.GetAsync(new Uri("/_localstack/health", UriKind.Relative), cts.Token);
        var laterHealthContent = await laterHealthResponse.Content.ReadFromJsonAsync<JsonNode>(cts.Token);
        Assert.Equal(HttpStatusCode.OK, laterHealthResponse.StatusCode);

        var sqsServicesNode = laterHealthContent?["services"]?.AsObject();
        Assert.NotNull(sqsServicesNode);
        Assert.True(sqsServicesNode.ContainsKey("sqs"));
        Assert.Equal("running", sqsServicesNode["sqs"]?.ToString());
    }

    [Fact]
    public async Task LocalStack_Should_Eagerly_Load_Services_When_Configured_Async()
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

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
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var servicesNode = healthContent?["services"]?.AsObject();
        Assert.NotNull(servicesNode);
        Assert.True(servicesNode.ContainsKey("sqs"));
        Assert.Equal("running", servicesNode["sqs"]?.ToString());
    }

    [Fact]
    public async Task LocalStack_Should_Eagerly_Load_Multiple_Services_Async()
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

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
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var servicesNode = healthContent?["services"]?.AsObject();
        Assert.NotNull(servicesNode);

        // All three services should be running
        Assert.True(servicesNode.ContainsKey("sqs"));
        Assert.Equal("running", servicesNode["sqs"]?.ToString());
        Assert.True(servicesNode.ContainsKey("dynamodb"));
        Assert.Equal("running", servicesNode["dynamodb"]?.ToString());
        Assert.True(servicesNode.ContainsKey("s3"));
        Assert.Equal("running", servicesNode["s3"]?.ToString());
    }

    [Fact]
    public async Task LocalStack_Should_Handle_Empty_EagerLoadedServices_Like_Lazy_Loading_Async()
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

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
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var servicesNode = healthContent?["services"]?.AsObject();
        Assert.NotNull(servicesNode);

        // Services should not be running by default (lazy loading)
        if (servicesNode.ContainsKey("sqs"))
        {
            Assert.NotEqual("running", servicesNode["sqs"]?.ToString());
        }
    }

    [Fact]
    public void LocalStack_Should_Throw_When_Env_Var_Collision_With_SERVICES()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["SERVICES"] = "lambda,s3";
                container.EagerLoadedServices = [AwsService.Sqs];
            }));

        Assert.Contains("Cannot set 'SERVICES'", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AdditionalEnvironmentVariables", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalStack_Should_Throw_When_Env_Var_Collision_With_EAGER_SERVICE_LOADING()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
            {
                container.AdditionalEnvironmentVariables["EAGER_SERVICE_LOADING"] = "1";
                container.EagerLoadedServices = [AwsService.Sqs];
            }));

        Assert.Contains("EAGER_SERVICE_LOADING", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AdditionalEnvironmentVariables", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
