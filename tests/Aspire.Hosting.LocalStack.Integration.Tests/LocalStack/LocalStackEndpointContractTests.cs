namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

[NotInParallel("IntegrationTests")]
public class LocalStackEndpointContractTests
{
    private const string ConsumerImage = "curlimages/curl";
    private const string ConsumerTag = "8.23.0";
    private const string ConsumerDigest = "3fb7e13e5d179a845c97d78204f3f1064d1907037f26a3ebf91710571151fd93";
    private const string SettingsMarker = "consumer-setting ";
    private const int ConnectFailedExitCode = 7;

    /// <summary>
    /// Sends the request the way a LocalStack.Client S3 client does: LocalStack is the HTTP proxy
    /// for the regional AWS endpoint, signed with the injected session. The settings are printed
    /// first so the test can rebuild the same client from exactly what the container received.
    /// </summary>
    private const string ConsumerScript = """
        set -eu
        env | grep '^LocalStack__' | sort | sed 's/^/consumer-setting /'
        response=$(curl -sS --fail --max-time 30 \
          --aws-sigv4 "aws:amz:${LocalStack__Session__RegionName}:s3" \
          --user "${LocalStack__Session__AwsAccessKeyId}:${LocalStack__Session__AwsAccessKey}" \
          -H "x-amz-security-token: ${LocalStack__Session__AwsSessionToken}" \
          --proxy "http://${LocalStack__Config__LocalStackHost}:${LocalStack__Config__EdgePort}" \
          "http://s3.${LocalStack__Session__RegionName}.amazonaws.com/")
        printf '%s\n' "$response" | grep -F -q "<Name>${EXPECTED_BUCKET}</Name>" || exit 24
        """;

    [Test]
    public async Task Host_And_Managed_Container_Client_Requests_Use_Their_Own_Network(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("endpoint-contract-");
        try
        {
            await VerifyConsumersAsync(directory.FullName, cancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task VerifyConsumersAsync(string directory, CancellationToken cancellationToken)
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
        await runtime.WriteImageReceiptAsync(localStack!.Resource, "endpoint-contract", runtime.OutputDirectory, timeout.Token);
        var template = Path.Combine(directory, "seed.json");
        await File.WriteAllTextAsync(template, """
            {"Resources":{"Marker":{"Type":"AWS::S3::Bucket"}},"Outputs":{"BucketName":{"Value":{"Ref":"Marker"}}}}
            """, timeout.Token);
        var seed = builder.AddAWSCloudFormationTemplate("seed-" + Guid.NewGuid().ToString("N"), template).WithReference(localStack);
        var host = builder.AddExecutable("host-consumer", "dotnet", directory, "--info").WithReference(localStack);
        var container = AddContainerConsumer(builder, "container-consumer", localStack, seed);
        var wrongHost = AddContainerConsumer(builder, "wrong-host", localStack, seed);
        var wrongPort = AddContainerConsumer(builder, "wrong-port", localStack, seed);
        // Late ordinary Core bindings deliberately override only the controlled negative consumers.
        localStack.OnConnectionStringAvailable((_, _, _) =>
        {
            wrongHost.WithEnvironment("LocalStack__Config__LocalStackHost", "localhost");
            wrongPort.WithEnvironment("LocalStack__Config__EdgePort", "1");
            return Task.CompletedTask;
        });
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        var hostSettings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, exitCode) in new[] { (host.Resource.Name, 0), ("container-consumer", 0), ("wrong-host", ConnectFailedExitCode), ("wrong-port", ConnectFailedExitCode) })
        {
            var result = await notifications.WaitForResourceAsync(name,
                update => KnownResourceStates.TerminalStates.Contains(update.Snapshot.State?.Text ?? string.Empty, StringComparer.Ordinal), timeout.Token);
            await Assert.That(result.Snapshot.ExitCode).IsEqualTo(exitCode);
            if (name == host.Resource.Name)
            {
                // The orchestrator reports the environment it actually started the process with.
                foreach (var variable in result.Snapshot.EnvironmentVariables.Where(entry => entry.Name.StartsWith("LocalStack__", StringComparison.Ordinal)))
                {
                    hostSettings[variable.Name] = variable.Value ?? string.Empty;
                }
            }
        }

        if (hostSettings.Count == 0)
        {
            throw new InvalidOperationException("The host consumer was started without LocalStack settings.");
        }

        var bucket = seed.Resource.Outputs!.Single(output => output.OutputKey == "BucketName").OutputValue;
        using (var client = LocalStackTestHelpers.CreateConsumerSession(hostSettings).CreateClientByImplementation<AmazonS3Client>())
        {
            ((AmazonS3Config)client.Config).MaxErrorRetry = 0;
            var response = await client.ListBucketsAsync(timeout.Token);
            await Assert.That(response.HttpStatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Buckets.Select(entry => entry.BucketName)).Contains(bucket);
        }

        var containerSettings = await ReadPrintedSettingsAsync(app, container.Resource, timeout.Token);
        await Assert.That(containerSettings["LocalStack__Config__LocalStackHost"])
            .IsNotEqualTo(hostSettings["LocalStack__Config__LocalStackHost"]);
        using (var client = LocalStackTestHelpers.CreateConsumerSession(containerSettings).CreateClientByImplementation<AmazonS3Client>())
        {
            await Assert.That(client.Config.ProxyHost).IsEqualTo(containerSettings["LocalStack__Config__LocalStackHost"]);
            await Assert.That(client.Config.ProxyPort.ToString(CultureInfo.InvariantCulture))
                .IsEqualTo(containerSettings["LocalStack__Config__EdgePort"]);
            await Assert.That(client.Config.UseHttp).IsTrue();
            await Assert.That(client.Config.ServiceURL).IsNull();
            await Assert.That(client.Config.RegionEndpoint?.SystemName).IsEqualTo(containerSettings["LocalStack__Session__RegionName"]);
        }
    }

    private static IResourceBuilder<ContainerResource> AddContainerConsumer(
        IDistributedApplicationBuilder builder, string name,
        IResourceBuilder<ILocalStackResource> localStack, IResourceBuilder<ICloudFormationTemplateResource> seed)
        => builder.AddContainer(name, ConsumerImage, ConsumerTag).WithImageSHA256(ConsumerDigest)
            .WithEntrypoint("/bin/sh").WithArgs("-c", ConsumerScript).WithReference(localStack).WaitFor(seed)
            .WithEnvironment("EXPECTED_BUCKET", seed.GetOutput("BucketName"));

    private static async Task<Dictionary<string, string>> ReadPrintedSettingsAsync(
        DistributedApplication app, ContainerResource resource, CancellationToken cancellationToken)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        var logs = app.Services.GetRequiredService<ResourceLoggerService>();
        await foreach (var batch in logs.GetAllAsync(resource).WithCancellation(cancellationToken))
        {
            foreach (var line in batch)
            {
                var start = line.Content.IndexOf(SettingsMarker, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                var setting = line.Content[(start + SettingsMarker.Length)..];
                var separator = setting.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    throw new InvalidOperationException($"The container printed a malformed setting: {line.Content}");
                }

                settings[setting[..separator]] = setting[(separator + 1)..];
            }
        }

        return settings.Count > 0
            ? settings
            : throw new InvalidOperationException($"The container {resource.Name} printed no LocalStack settings.");
    }
}
