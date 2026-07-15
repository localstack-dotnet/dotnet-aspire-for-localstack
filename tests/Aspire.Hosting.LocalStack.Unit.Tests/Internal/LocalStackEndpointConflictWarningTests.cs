using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackEndpointConflictWarningTests
{
    private const string EndpointValue = "http://native-endpoint:4566";

    [Test]
    [Arguments("AWS_ENDPOINT_URL")]
    [Arguments("AWS_ENDPOINT_URL_SQS")]
    [Arguments("aws_endpoint_url_sqs")]
    public async Task Warns_Once_When_LocalStack_Client_Proxy_And_Native_Endpoint_Are_Configured(string endpointKey)
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            worker.WithEnvironment("LocalStack__UseLocalStack", "true");
            worker.WithEnvironment(endpointKey, EndpointValue);
            worker.WithEnvironment("AWS_SECRET_ACCESS_KEY", "native-secret");

            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        var result = await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(1);
        await Assert.That(logLines.Single(IsWarningLog)).Contains("worker");
        await Assert.That(logLines.Single(IsWarningLog)).Contains("Choose either LocalStack.Client proxy routing or native AWS SDK endpoint routing");
        await Assert.That(logLines.Single(IsWarningLog)).DoesNotContain(EndpointValue);
        await Assert.That(logLines.Single(IsWarningLog)).DoesNotContain("native-secret");
        await Assert.That(result.EnvironmentVariables.ToDictionary(StringComparer.Ordinal)).ContainsKey(endpointKey);
    }

    [Test]
    [Arguments("false")]
    [Arguments("")]
    public async Task Does_Not_Warn_When_LocalStack_Client_Proxy_Is_False_Or_Missing(string useLocalStackValue)
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            if (useLocalStackValue.Length > 0)
            {
                worker.WithEnvironment("LocalStack__UseLocalStack", useLocalStackValue);
            }

            worker.WithEnvironment("AWS_ENDPOINT_URL", EndpointValue);

            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(0);
    }

    [Test]
    public async Task Does_Not_Warn_When_Native_Endpoint_Is_Configured_Without_LocalStack_Enabled_Annotation()
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            builder.AddResource(new ExecutableResource("worker", "run-worker", "."))
                .WithEnvironment("LocalStack__UseLocalStack", "true")
                .WithEnvironment("AWS_ENDPOINT_URL", EndpointValue);

            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(0);
    }

    [Test]
    public async Task Warning_Callback_Does_Not_Change_Environment()
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            worker.WithEnvironment("LocalStack__UseLocalStack", "true");
            worker.WithEnvironment("AWS_ENDPOINT_URL_SQS", EndpointValue);

            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        var afterStartEnvironment = await BuildEnvironment(worker);

        await Assert.That(afterStartEnvironment.EnvironmentVariables.ToArray()).IsEquivalentTo(
        [
            new KeyValuePair<string, string>("LocalStack__UseLocalStack", "true"),
            new KeyValuePair<string, string>("AWS_ENDPOINT_URL_SQS", EndpointValue),
        ]);
    }

    [Test]
    public async Task Duplicate_Registration_Adds_One_Marker_Annotation_And_One_Warning()
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            worker.WithEnvironment("LocalStack__UseLocalStack", "true");
            worker.WithEnvironment("AWS_ENDPOINT_URL", EndpointValue);

            builder.UseLocalStack(localStack);
            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(worker.Annotations.OfType<LocalStackEndpointConflictWarningAnnotation>()).HasSingleItem();
        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(1);
    }

    [Test]
    public async Task Endpoint_Callback_Appended_After_Warning_Registration_Is_Not_Observed()
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            worker.WithEnvironment("LocalStack__UseLocalStack", "true");

            builder.UseLocalStack(localStack);

            builder.OnBeforeStart((_, _) =>
            {
                worker.WithEnvironment("AWS_ENDPOINT_URL", EndpointValue);
                return Task.CompletedTask;
            });
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        var result = await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(result.EnvironmentVariables.ToDictionary(StringComparer.Ordinal)).ContainsKey("AWS_ENDPOINT_URL");
        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(0);
    }

    [Test]
    public async Task Does_Not_Warn_For_Endpoint_Key_That_Only_Shares_The_Prefix_Text()
    {
        await using var app = CreateAppWithWorker(builder =>
        {
            var localStack = builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true));
            var worker = AddAnnotatedWorker(builder, localStack);

            worker.WithEnvironment("LocalStack__UseLocalStack", "true");
            worker.WithEnvironment("AWS_ENDPOINT_URLISH", EndpointValue);

            builder.UseLocalStack(localStack);
        });

        var worker = app.GetResource<ExecutableResource>("worker");

        await PublishBeforeStart(app);
        await BuildEnvironment(worker);
        var logLines = GetLogs(app, worker);

        await Assert.That(logLines.Count(IsWarningLog)).IsEqualTo(0);
    }

    private static DistributedApplication CreateAppWithWorker(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = DistributedApplication.CreateBuilder(["--AppHost:Operation=publish"]);
        configure(builder);
        return builder.Build();
    }

    private static IResourceBuilder<ExecutableResource> AddAnnotatedWorker(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<ILocalStackResource>? localStack)
    {
        var worker = builder.AddResource(new ExecutableResource("worker", "run-worker", "."));
        worker.Resource.Annotations.Add(new LocalStackEnabledAnnotation(localStack?.Resource ?? throw new InvalidOperationException("LocalStack resource was not created.")));

        return worker;
    }

    private static async Task PublishBeforeStart(DistributedApplication app)
    {
        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), CancellationToken.None);
    }

    private static Task<IExecutionConfigurationResult> BuildEnvironment(IResource resource)
        => ExecutionConfigurationBuilder.Create(resource)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), NullLogger.Instance, CancellationToken.None);

    private static List<string> GetLogs(DistributedApplication app, ExecutableResource resource)
    {
        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var loggersProperty = typeof(ResourceLoggerService).GetProperty("Loggers", BindingFlags.Instance | BindingFlags.NonPublic)
                              ?? throw new InvalidOperationException("ResourceLoggerService.Loggers was not found.");
        var loggerStates = (System.Collections.IDictionary?)loggersProperty.GetValue(loggerService)
                           ?? throw new InvalidOperationException("ResourceLoggerService.Loggers was null.");
        var loggerState = loggerStates[resource.Name];

        if (loggerState is null)
        {
            return [];
        }

        var entriesField = loggerState.GetType().GetField("_inMemoryEntries", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?? throw new InvalidOperationException("ResourceLoggerState._inMemoryEntries was not found.");
        var entries = (System.Collections.IEnumerable?)entriesField.GetValue(loggerState)
                      ?? throw new InvalidOperationException("ResourceLoggerState._inMemoryEntries was null.");
        var logs = new List<string>();

        foreach (var entry in entries)
        {
            var contentProperty = entry.GetType().GetProperty("Content")
                                  ?? throw new InvalidOperationException("LogEntry.Content was not found.");
            logs.Add((string?)contentProperty.GetValue(entry) ?? string.Empty);
        }

        return logs;
    }

    private static bool IsWarningLog(string logLine)
        => logLine.Contains("LocalStack.Client proxy routing", StringComparison.Ordinal);
}
