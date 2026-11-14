using System.Runtime.CompilerServices;

namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

[NotInParallel("IntegrationTests")]
public class LocalStackEndpointContractTests
{
    private const string RuntimeImage = "mcr.microsoft.com/dotnet/aspnet";
    private const string RuntimeDigest = "d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac";

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
        var probeDirectory = GetProbeDirectory();
        builder.AddExecutable("host-probe", "dotnet", probeDirectory, "LocalStack.EndpointProbe.dll")
            .WithReference(localStack).WaitFor(seed).WithEnvironment("EXPECTED_BUCKET", seed.GetOutput("BucketName"));
        AddContainerProbe(builder, "container-probe", probeDirectory, localStack, seed);
        var wrongHost = AddContainerProbe(builder, "wrong-host", probeDirectory, localStack, seed)
            .WithEnvironment("EXPECTED_FAILURE", "true");
        var wrongPort = AddContainerProbe(builder, "wrong-port", probeDirectory, localStack, seed)
            .WithEnvironment("EXPECTED_FAILURE", "true");
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
        foreach (var name in new[] { "host-probe", "container-probe", "wrong-host", "wrong-port" })
        {
            var result = await notifications.WaitForResourceAsync(name,
                update => KnownResourceStates.TerminalStates.Contains(update.Snapshot.State?.Text ?? string.Empty, StringComparer.Ordinal), timeout.Token);
            await Assert.That(result.Snapshot.ExitCode).IsEqualTo(name.StartsWith("wrong-", StringComparison.Ordinal) ? 23 : 0);
        }
    }

    private static IResourceBuilder<ContainerResource> AddContainerProbe(
        IDistributedApplicationBuilder builder, string name, string directory,
        IResourceBuilder<ILocalStackResource> localStack, IResourceBuilder<ICloudFormationTemplateResource> seed)
        => builder.AddContainer(name, RuntimeImage).WithImageSHA256(RuntimeDigest)
            .WithBindMount(directory, "/probe", isReadOnly: true).WithEntrypoint("dotnet")
            .WithArgs("/probe/LocalStack.EndpointProbe.dll").WithReference(localStack).WaitFor(seed)
            .WithEnvironment("EXPECTED_BUCKET", seed.GetOutput("BucketName"));

    private static string GetProbeDirectory([CallerFilePath] string sourceFile = "")
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Test build configuration is unavailable.");
        var path = Path.GetFullPath($"../../../playground/LocalStack.EndpointProbe/bin/{configuration}/net10.0",
            Path.GetDirectoryName(sourceFile) ?? throw new InvalidOperationException("Test source location is unavailable."));
        if (!File.Exists(Path.Combine(path, "LocalStack.EndpointProbe.dll")))
        {
            throw new InvalidOperationException("The endpoint consumer probe was not built.");
        }

        return path;
    }
}
