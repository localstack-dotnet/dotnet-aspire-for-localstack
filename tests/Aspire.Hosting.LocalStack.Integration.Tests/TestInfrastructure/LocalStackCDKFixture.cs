namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Test fixture for LocalStack CDK provisioning integration tests.
/// Starts the AppHost once and shares it across all tests in the collection.
/// </summary>
public sealed class LocalStackCdkFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private CloudFormationStackOutputs? _stackOutputs;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not initialized");

    public string LocalStackConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// AWS region extracted from the LocalStack resource configuration.
    /// </summary>
    public string RegionName { get; private set; } = string.Empty;

    public CloudFormationStackOutputs StackOutputs =>
        _stackOutputs ?? throw new InvalidOperationException("Stack outputs not initialized");

    public async ValueTask InitializeAsync()
    {
        using var parentCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token, TestContext.Current.CancellationToken);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LocalStack_Provisioning_CDK_AppHost>(["LocalStack:UseLocalStack=true"], cts.Token);

        // Configure logging to capture Aspire app logs in xUnit test output
        appHost.Services.AddLogging(logging =>
        {
            if (TestContext.Current.TestOutputHelper is not null)
            {
                logging.AddXUnit(TestContext.Current.TestOutputHelper);
            }
            logging.SetMinimumLevel(LogLevel.Information)
                   .AddFilter("Aspire.Hosting.Dcp", LogLevel.Warning);
        });

        _app = await appHost.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);

        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();

        // Wait for LocalStack to be healthy
        await resourceNotificationService.WaitForResourceHealthyAsync("localstack", cts.Token);
        await resourceNotificationService.WaitForResourceAsync("custom", KnownResourceStates.Running, cts.Token);
        await resourceNotificationService.WaitForResourceAsync("CDKBootstrap", KnownResourceStates.Running, cts.Token);

        // Get LocalStack connection string and region
        var localStackResource = appHost.Resources.OfType<ILocalStackResource>().FirstOrDefault()
                                 ?? throw new InvalidOperationException("LocalStack resource not found");
        LocalStackConnectionString = await _app.GetConnectionStringAsync("localstack", cancellationToken: cts.Token)
                                     ?? throw new InvalidOperationException("LocalStack connection string is null");
        RegionName = localStackResource.Options.Session.RegionName
                     ?? throw new InvalidOperationException("LocalStack region not configured");

        // Extract CloudFormation outputs
        _stackOutputs = await LocalStackTestHelpers.WaitForStackOutputsAsync(
            resourceNotificationService,
            "custom",
            cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

/// <summary>
/// xUnit collection definition for CDK provisioning tests to share the fixture.
/// </summary>
[CollectionDefinition("LocalStackCDK", DisableParallelization = true)]
public sealed class LocalStackCdkCollectionDefinition : ICollectionFixture<LocalStackCdkFixture>;
