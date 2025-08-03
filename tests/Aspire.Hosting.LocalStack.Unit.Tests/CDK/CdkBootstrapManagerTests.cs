using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.LocalStack.CDK;

namespace Aspire.Hosting.LocalStack.Unit.Tests.CDK;

[Collection("SequentialCdkTests")]
public sealed class CdkBootstrapManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public CdkBootstrapManagerTests()
    {
        _testDirectory = CdkBootstrapManager.TempDirPath;
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Return_Valid_File_Path()
    {
        var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath();

        Assert.NotNull(templatePath);
        Assert.NotEmpty(templatePath);
        Assert.True(Path.IsPathFullyQualified(templatePath));
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Extract_Template_To_Temp_Directory()
    {
        var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath();

        Assert.Contains(Path.GetTempPath(), templatePath, StringComparison.Ordinal);
        Assert.Contains("aspire-localstack", templatePath, StringComparison.Ordinal);
        Assert.EndsWith(".template", templatePath, StringComparison.Ordinal);
        Assert.Contains("cdk-bootstrap-", templatePath, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_File_That_Exists()
    {
        var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath();

        Assert.True(File.Exists(templatePath));
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_Non_Empty_Template_File()
    {
        var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath();

        var fileInfo = new FileInfo(templatePath);
        Assert.True(fileInfo.Length > 0);
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Return_Same_Path_On_Multiple_Calls()
    {
        var path1 = CdkBootstrapManager.GetBootstrapTemplatePath();
        var path2 = CdkBootstrapManager.GetBootstrapTemplatePath();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_Directory_If_It_Does_Not_Exist()
    {
        Assert.False(Directory.Exists(_testDirectory));

        var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath();

        Assert.True(Directory.Exists(_testDirectory));
        Assert.True(File.Exists(templatePath));
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Handle_Multiple_Sequential_Calls()
    {
        var results = new List<string>();
        const int sequentialCalls = 5;

        for (var i = 0; i < sequentialCalls; i++)
        {
            results.Add(CdkBootstrapManager.GetBootstrapTemplatePath());
        }

        Assert.All(results, Assert.NotNull);
        Assert.All(results, path => Assert.True(File.Exists(path)));
        Assert.True(results.TrueForAll(path => string.Equals(path, results[0], StringComparison.Ordinal)));
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Overwrite_Existing_File()
    {
        var firstPath = CdkBootstrapManager.GetBootstrapTemplatePath();
        var originalContent = File.ReadAllText(firstPath);

        File.WriteAllText(firstPath, "outdated content");

        var secondPath = CdkBootstrapManager.GetBootstrapTemplatePath();
        var newContent = File.ReadAllText(secondPath);

        Assert.Equal(firstPath, secondPath);
        Assert.Equal(originalContent, newContent);
        Assert.NotEqual("outdated content", newContent);
    }

    [Fact]
    public async Task GetBootstrapTemplatePath_Should_Be_Thread_Safe_And_Handle_Parallel_Calls()
    {
        const int parallelTasks = 20;
        var tasks = new List<Task<string>>();

        for (var i = 0; i < parallelTasks; i++)
        {
            tasks.Add(Task.Run(CdkBootstrapManager.GetBootstrapTemplatePath));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(parallelTasks, results.Length);
        Assert.All(results, path => Assert.False(string.IsNullOrEmpty(path)));

        var firstPath = results[0];
        Assert.All(results, path => Assert.Equal(firstPath, path));

        Assert.True(File.Exists(firstPath));
        var fileInfo = new FileInfo(firstPath);
        Assert.True(fileInfo.Length > 0);
    }
}

[CollectionDefinition("CdkManagerSequential", DisableParallelization = true)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
[SuppressMessage("Design", "MA0048:File name must match type name")]
public class CdkManagerSequentialCollection : ICollectionFixture<CdkBootstrapManagerTests>;
