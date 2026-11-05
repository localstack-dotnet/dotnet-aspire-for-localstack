namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

public class ManifestActivationTests
{
    [Test]
    public async Task Private_Scalar_Is_Not_A_Published_Manifest_Default()
    {
        var directory = Directory.CreateTempSubdirectory("localstack-manifest-");
        try
        {
            var path = Path.Combine(directory.FullName, "manifest.json");
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = ["--publisher", "manifest", "--output-path", path],
                ProjectDirectory = directory.FullName,
                DisableDashboard = true,
            });
            await Assert.That(builder.ExecutionContext.IsPublishMode).IsTrue();
            builder.AddParameter("visible-input", "non-sensitive-control", publishValueAsDefault: true);
            builder.AddLocalStack("localstack", awsConfig: null, options => options.WithEnabled(true),
                container => container.WithAuthToken("probe-manifest-secret"));
            await using var app = builder.Build();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await app.RunAsync(cancellation.Token);
            var json = await File.ReadAllTextAsync(path, cancellation.Token);
            using var document = JsonDocument.Parse(json);
            var resources = document.RootElement.GetProperty("resources");

            await Assert.That(json).DoesNotContain("probe-manifest-secret");
            await Assert.That(resources.TryGetProperty("localstack", out _)).IsFalse();
            await Assert.That(resources.TryGetProperty("visible-input", out _)).IsTrue();
            await Assert.That(builder.Resources.OfType<ParameterResource>().Count()).IsEqualTo(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
