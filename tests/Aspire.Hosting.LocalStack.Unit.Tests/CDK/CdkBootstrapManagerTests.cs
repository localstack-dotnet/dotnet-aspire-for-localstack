namespace Aspire.Hosting.LocalStack.Unit.Tests.CDK;

[Collection("SequentialCdkTests")]
public sealed class CdkBootstrapManagerTests
{
    [Fact]
    public void GetBootstrapTemplatePath_Should_Return_Valid_File_Path()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            Assert.NotNull(templatePath);
            Assert.NotEmpty(templatePath);
            Assert.True(Path.IsPathFullyQualified(templatePath));
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Extract_Template_To_Temp_Directory()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            Assert.Contains(testDir, templatePath, StringComparison.Ordinal);
            Assert.EndsWith(".template", templatePath, StringComparison.Ordinal);
            Assert.Contains("cdk-bootstrap-", templatePath, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_File_That_Exists()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            Assert.True(File.Exists(templatePath));
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_Non_Empty_Template_File()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            var fileInfo = new FileInfo(templatePath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Return_Same_Path_On_Multiple_Calls()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var path1 = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var path2 = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            Assert.Equal(path1, path2);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Create_Directory_If_It_Does_Not_Exist()
    {
        var testDir = CreateTestDirectory();
        // Delete the directory to test creation
        Directory.Delete(testDir, recursive: true);

        try
        {
            Assert.False(Directory.Exists(testDir));

            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            Assert.True(Directory.Exists(testDir));
            Assert.True(File.Exists(templatePath));
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Handle_Multiple_Sequential_Calls()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var results = new List<string>();
            const int sequentialCalls = 5;

            for (var i = 0; i < sequentialCalls; i++)
            {
                results.Add(CdkBootstrapManager.GetBootstrapTemplatePath(testDir));
            }

            Assert.All(results, Assert.NotNull);
            Assert.All(results, path => Assert.True(File.Exists(path)));
            Assert.True(results.TrueForAll(path => string.Equals(path, results[0], StringComparison.Ordinal)));
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public void GetBootstrapTemplatePath_Should_Overwrite_Existing_File()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var firstPath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var originalContent = File.ReadAllText(firstPath);

            File.WriteAllText(firstPath, "outdated content");

            var secondPath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var newContent = File.ReadAllText(secondPath);

            Assert.Equal(firstPath, secondPath);
            Assert.Equal(originalContent, newContent);
            Assert.NotEqual("outdated content", newContent);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Fact]
    public async Task GetBootstrapTemplatePath_Should_Be_Thread_Safe_And_Handle_Parallel_Calls()
    {
        var testDir = CreateTestDirectory();
        try
        {
            const int parallelTasks = 20;
            var tasks = new List<Task<string>>();

            for (var i = 0; i < parallelTasks; i++)
            {
                tasks.Add(Task.Run(() => CdkBootstrapManager.GetBootstrapTemplatePath(testDir)));
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
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    private static string CreateTestDirectory()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "localstack-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        return testDir;
    }

    private static void CleanupTestDirectory(string testDirectory)
    {
        if (!Directory.Exists(testDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(testDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Ignore cleanup errors to prevent test failures
        }
    }
}

[CollectionDefinition("CdkManagerSequential", DisableParallelization = true)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
[SuppressMessage("Design", "MA0048:File name must match type name")]
public class CdkManagerSequentialCollection : ICollectionFixture<CdkBootstrapManagerTests>;
