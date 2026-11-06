namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

public class RuntimeTestConfigurationTests
{
    [Test]
    public async Task Selector_Defaults_To_Current_And_Rejects_Unknown()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "runtime-images.json");

        var configuration = await RuntimeTestConfiguration.ReadAsync(null, path);

        await Assert.That(configuration.Profile).IsEqualTo(RuntimeTestProfile.Current);
        await Assert.That(configuration.ExpectedImage.Tag).IsEqualTo("2026.10.6");
        foreach (var invalid in new[] { "", " ", "CURRENT", "unknown" })
        {
            await Assert.That(async () => await RuntimeTestConfiguration.ReadAsync(invalid, path)).ThrowsExactly<ArgumentException>();
        }
    }

    [Test]
    public async Task Legacy_Uses_Preset_Not_RawOverride()
    {
        var configuration = await RuntimeTestConfiguration.ReadAsync("legacy",
            Path.Combine(AppContext.BaseDirectory, "runtime-images.json"));
        var options = new LocalStackContainerOptions();

        var returned = configuration.ConfigureContainer(options);
        var args = configuration.CreateAppHostArguments();
        var builder = DistributedApplication.CreateBuilder([]);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            hosting => hosting.WithEnabled(true), container => configuration.ConfigureContainer(container));

        await Assert.That(returned).IsSameReferenceAs(options);
        await Assert.That(options.ContainerRegistry).IsNull();
        await Assert.That(options.ContainerImage).IsNull();
        await Assert.That(options.ContainerImageTag).IsNull();
        await Assert.That(args).Contains("Aspire:Hosting:LocalStack:Enabled=true");
        await Assert.That(args).Contains("Playground:LocalStack:UseLegacy=true");
        await Assert.That(localStack).IsNotNull();
        await Assert.That(localStack!.Resource.Annotations.OfType<ContainerImageAnnotation>().Single().Tag)
            .IsEqualTo(configuration.ExpectedImage.Tag);
    }

    [Test]
    public async Task Expected_Image_Comes_From_Qualification_Record()
    {
        var directory = Directory.CreateTempSubdirectory("runtime-oracle-");
        try
        {
            var path = Path.Combine(directory.FullName, "images.json");
            await File.WriteAllTextAsync(path, """
                {"schema_version":1,"images":{"current":{
                  "registry":"registry.example","image":"team/emulator","tag":"qualified-test","digest":null
                }}}
                """);

            var configuration = await RuntimeTestConfiguration.ReadAsync("current", path, directory.FullName);

            await Assert.That(configuration.ExpectedImage.Registry).IsEqualTo("registry.example");
            await Assert.That(configuration.ExpectedImage.Image).IsEqualTo("team/emulator");
            await Assert.That(configuration.ExpectedImage.Tag).IsEqualTo("qualified-test");
            await Assert.That(configuration.OutputDirectory).IsEqualTo(directory.FullName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Selected_Runtime_Should_Match_Qualified_Identity_And_Write_Unique_Receipts()
    {
        var directory = Directory.CreateTempSubdirectory("runtime-receipts-");
        try
        {
            var configuration = await RuntimeTestConfiguration.ReadAsync("legacy",
                Path.Combine(AppContext.BaseDirectory, "runtime-images.json"), directory.FullName);
            var builder = DistributedApplication.CreateBuilder([]);
            var localStack = builder.AddLocalStack("localstack", awsConfig: null,
                hosting => hosting.WithEnabled(true), container => configuration.ConfigureContainer(container));
            await Assert.That(localStack).IsNotNull();

            var actual = RuntimeTestConfiguration.ReadEffectiveImage(localStack!.Resource);
            var first = await configuration.WriteImageReceiptAsync(localStack.Resource, "pure-model", directory.FullName);
            var second = await configuration.WriteImageReceiptAsync(localStack.Resource, "pure-model", directory.FullName);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(first));

            await Assert.That(actual).IsEqualTo(configuration.ExpectedImage);
            await Assert.That(first).IsNotEqualTo(second);
            await Assert.That(document.RootElement.GetProperty("runtime_id").GetString()).IsEqualTo("legacy");
            await Assert.That(document.RootElement.GetProperty("scenario_group").GetString()).IsEqualTo("pure-model");
            await Assert.That(document.RootElement.GetProperty("image").GetProperty("tag").GetString()).IsEqualTo("4.14.0");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Process_Profile_Is_Selected_Once_And_Shared_Without_Mutating_Environment()
    {
        var first = await RuntimeTestConfiguration.ForCurrentProcessAsync();
        var second = await RuntimeTestConfiguration.ForCurrentProcessAsync();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(Path.IsPathFullyQualified(first.OutputDirectory)).IsTrue();
    }

    [Test]
    public async Task Receipts_Carry_The_Supplied_Execution_Id()
    {
        var directory = Directory.CreateTempSubdirectory("bound-receipts-");
        try
        {
            var configuration = await RuntimeTestConfiguration.ReadAsync("legacy",
                Path.Combine(AppContext.BaseDirectory, "runtime-images.json"), directory.FullName, executionId: "supplied-execution");
            var builder = DistributedApplication.CreateBuilder([]);
            var localStack = builder.AddLocalStack("localstack", awsConfig: null,
                hosting => hosting.WithEnabled(true), container => configuration.ConfigureContainer(container));
            var first = await configuration.WriteImageReceiptAsync(localStack!.Resource, "pure-model", directory.FullName);
            var second = await configuration.WriteImageReceiptAsync(localStack.Resource, "pure-model", directory.FullName);
            await Assert.That(first).IsNotEqualTo(second);
            foreach (var path in new[] { first, second })
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                var receipt = document.RootElement;
                await Assert.That(receipt.GetProperty("schema_version").GetInt32()).IsEqualTo(2);
                await Assert.That(receipt.GetProperty("execution_id").GetString()).IsEqualTo("supplied-execution");
                await Assert.That(receipt.GetProperty("runtime_id").GetString()).IsEqualTo("legacy");
                await Assert.That(receipt.GetProperty("scenario_group").GetString()).IsEqualTo("pure-model");
                var image = receipt.GetProperty("image");
                await Assert.That(image.GetProperty("registry").GetString()).IsEqualTo(configuration.ExpectedImage.Registry);
                await Assert.That(image.GetProperty("image").GetString()).IsEqualTo(configuration.ExpectedImage.Image);
                await Assert.That(image.GetProperty("tag").GetString()).IsEqualTo(configuration.ExpectedImage.Tag);
                await Assert.That(image.GetProperty("digest").GetString()).IsEqualTo(configuration.ExpectedImage.Digest);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

}
