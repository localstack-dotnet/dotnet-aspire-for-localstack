using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

public class ActivationGuardStartupTests
{
    private const string ResourceName = "localstack-guard";
    private const string GuardError = "LocalStack resource 'localstack-guard' requires 'LOCALSTACK_AUTH_TOKEN'";

    [Test]
    public async Task Blank_Activation_Fails_Before_A_Container_Is_Created(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));
        var runtime = await RuntimeTestConfiguration.ForCurrentProcessAsync(cts.Token);

        // Create has no non-generic async overload; the sync overload is a builder factory (no I/O).
#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create(runtime.CreateAppHostArguments());
#pragma warning restore CA1849
        var localStack = builder.AddLocalStack(ResourceName, awsConfig: null,
            configureContainer: container => container.WithAuthToken(""));
        await Assert.That(localStack).IsNotNull();

        // The testing builder forwards each resource's log lines to an ILogger category named after the resource.
        using var capture = new GuardLogCapture();
        builder.Services.AddLogging(logging => logging.AddProvider(capture));
        await using var app = await builder.BuildAsync(cts.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var snapshots = new List<CustomResourceSnapshot>();

        // Aspire reports a resource-creation failure through the resource state; StartAsync itself returns.
        await Task.WhenAll(CollectSnapshotsUntilFailedAsync(notifications, snapshots, cts.Token), app.StartAsync(cts.Token));

        // Completes only when the guard's error was forwarded; the 2-minute bound fails the test otherwise.
        var guardLog = await capture.Found.Task.WaitAsync(cts.Token);
        await Assert.That(guardLog).Contains(GuardError);
        await Assert.That(snapshots[^1].State?.Text).IsEqualTo(KnownResourceStates.FailedToStart);
        foreach (var property in snapshots.SelectMany(snapshot => snapshot.Properties).Where(property => property.Name == "container.id"))
        {
            await Assert.That(property.Value?.ToString() ?? string.Empty).IsEqualTo(string.Empty);
        }
    }

    private static async Task CollectSnapshotsUntilFailedAsync(
        ResourceNotificationService notifications, List<CustomResourceSnapshot> snapshots, CancellationToken cancellationToken)
    {
        // Ends at FailedToStart; the 2-minute bound fails the test otherwise.
        await foreach (var update in notifications.WatchAsync(cancellationToken))
        {
            if (update.Resource.Name != ResourceName)
            {
                continue;
            }

            snapshots.Add(update.Snapshot);
            if (update.Snapshot.State?.Text == KnownResourceStates.FailedToStart)
            {
                return;
            }
        }
    }

    private sealed class GuardLogCapture : ILoggerProvider, ILogger
    {
        public TaskCompletionSource<string> Found { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ILogger CreateLogger(string categoryName) =>
            categoryName.EndsWith(".Resources." + ResourceName, StringComparison.Ordinal) ? this : NullLogger.Instance;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception) + exception;
            if (text.Contains(GuardError, StringComparison.Ordinal))
            {
                Found.TrySetResult(text);
            }
        }

        public void Dispose()
        {
        }
    }
}
