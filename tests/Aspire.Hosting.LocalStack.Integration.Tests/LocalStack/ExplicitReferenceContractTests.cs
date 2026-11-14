using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using InvalidOperationException = System.InvalidOperationException;

namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

[NotInParallel("IntegrationTests")]
public class ExplicitReferenceContractTests
{
    [Test]
    public async Task Explicit_Import_Is_ReadOnly_And_Bare_Workload_Receives_Client9(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("explicit-reference-");
        try
        {
            await VerifyExplicitContractsAsync(directory.FullName, cancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task VerifyExplicitContractsAsync(string directory, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var runtime = await RuntimeTestConfiguration.ForCurrentProcessAsync(timeout.Token);
#pragma warning disable CA1849
        await using var builder = DistributedApplicationTestingBuilder.Create(runtime.CreateAppHostArguments());
#pragma warning restore CA1849
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true).WithRegion("eu-central-1").WithCredentials("test", "test", "test"),
            container => runtime.ConfigureContainer(container));
        await Assert.That(localStack).IsNotNull();
        await runtime.WriteImageReceiptAsync(localStack!.Resource, "explicit-reference", runtime.OutputDirectory, timeout.Token);
        var template = Path.Combine(directory, "seed.json");
        await File.WriteAllTextAsync(template, """
            {"Resources":{"Marker":{"Type":"AWS::S3::Bucket"}},"Outputs":{"Witness":{"Value":"local-import-witness"}}}
            """, timeout.Token);
        var seed = builder.AddAWSCloudFormationTemplate("seed-" + Guid.NewGuid().ToString("N"), template)
            .WithReference(localStack);
        var imported = builder.AddAWSCloudFormationStack("imported", seed.Resource.StackName)
            .WithReference(localStack).WaitFor(seed);
        var worker = builder.AddExecutable("bare-worker", "dotnet", directory, "--info").WithReference(localStack);
        ReadOnlyCloudFormationRecorder? recorder = null;
        localStack.OnConnectionStringAvailable((_, _, _) =>
        {
            var client = imported.Resource.CloudFormationClient
                ?? throw new InvalidOperationException("Explicit import has no selected LocalStack client.");
            var wrapped = ReadOnlyCloudFormationRecorder.Wrap(client);
            recorder = wrapped.Recorder;
            recorder.Operations.Clear();
            imported.Resource.CloudFormationClient = wrapped.Client;
            return Task.CompletedTask;
        });
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceAsync("imported", KnownResourceStates.Running, timeout.Token);

        await Assert.That(imported.Resource.Outputs!.Single(output => output.OutputKey == "Witness").OutputValue)
            .IsEqualTo("local-import-witness");
        await Assert.That(recorder).IsNotNull();
        await Assert.That(recorder!.Operations.ToArray()).IsEquivalentTo(["DescribeStacksAsync"]);
        var connection = await app.GetConnectionStringAsync("localstack", cancellationToken: timeout.Token)
            ?? throw new InvalidOperationException("LocalStack connection string is missing.");
        await VerifyWorkerAsync(builder, worker.Resource, connection, timeout.Token);
        await VerifyImportFailureControlsAsync(connection, timeout.Token);
    }

    private static async Task VerifyImportFailureControlsAsync(string connection, CancellationToken cancellationToken)
    {
        var session = LocalStackTestHelpers.CreateLocalStackSession(connection, "eu-central-1");
        using var inner = session.CreateClientByImplementation<AmazonCloudFormationClient>();
        var wrapped = ReadOnlyCloudFormationRecorder.Wrap(inner);
        var request = new DescribeStacksRequest { StackName = "missing-" + Guid.NewGuid().ToString("N") };

        await Assert.That(async () => await wrapped.Client.DescribeStacksAsync(request, cancellationToken))
            .Throws<AmazonCloudFormationException>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(async () => await wrapped.Client.DescribeStacksAsync(request, cancelled.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(async () => await wrapped.Client.CreateStackAsync(new CreateStackRequest(), cancellationToken))
            .ThrowsExactly<InvalidOperationException>();

        await Assert.That(wrapped.Recorder.Operations.ToArray())
            .IsEquivalentTo(["DescribeStacksAsync", "DescribeStacksAsync", "CreateStackAsync"]);
    }

    private static async Task VerifyWorkerAsync(
        IDistributedApplicationBuilder builder, ExecutableResource worker, string connection, CancellationToken cancellationToken)
    {
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, worker);
        foreach (var annotation in worker.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context.EnvironmentVariables.Where(entry => entry.Key.StartsWith("LocalStack__", StringComparison.Ordinal)))
        {
            values[key] = value is IValueProvider provider
                ? await provider.GetValueAsync(cancellationToken) ?? string.Empty
                : (string)value;
        }

        var endpoint = new Uri(connection);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LocalStack__UseLocalStack"] = "True",
            ["LocalStack__Session__AwsAccessKeyId"] = "test",
            ["LocalStack__Session__AwsAccessKey"] = "test",
            ["LocalStack__Session__AwsSessionToken"] = "test",
            ["LocalStack__Session__RegionName"] = "eu-central-1",
            ["LocalStack__Config__LocalStackHost"] = endpoint.Host,
            ["LocalStack__Config__UseSsl"] = "False",
            ["LocalStack__Config__UseLegacyPorts"] = "False",
            ["LocalStack__Config__EdgePort"] = endpoint.Port.ToString(CultureInfo.InvariantCulture),
        };
        await Assert.That(values).IsEquivalentTo(expected);
        var config = new ConfigOptions(values["LocalStack__Config__LocalStackHost"],
            edgePort: int.Parse(values["LocalStack__Config__EdgePort"], CultureInfo.InvariantCulture));
        var session = SessionStandalone.Init().WithConfigurationOptions(config)
            .WithSessionOptions(new SessionOptions("test", "test", "test", "eu-central-1")).Create();
        using var client = session.CreateClientByImplementation<AmazonS3Client>();
        var response = await client.ListBucketsAsync(cancellationToken);
        await Assert.That(response.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Buckets).IsNotEmpty();
    }
}
