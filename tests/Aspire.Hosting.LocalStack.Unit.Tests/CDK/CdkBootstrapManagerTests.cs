namespace Aspire.Hosting.LocalStack.Unit.Tests.CDK;

public sealed class CdkBootstrapManagerTests
{
    [Test]
    public async Task GetBootstrapTemplatePath_Should_Return_Valid_File_Path()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            await Assert.That(templatePath).IsNotNull();
            await Assert.That(templatePath).IsNotEmpty();
            await Assert.That(Path.IsPathFullyQualified(templatePath)).IsTrue();
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Extract_Template_To_Temp_Directory()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            await Assert.That(templatePath).Contains(testDir);
            await Assert.That(templatePath).EndsWith(".template");
            await Assert.That(templatePath).Contains("cdk-bootstrap-");
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Create_File_That_Exists()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            await Assert.That(File.Exists(templatePath)).IsTrue();
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Create_Non_Empty_Template_File()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            var fileInfo = new FileInfo(templatePath);
            await Assert.That(fileInfo.Length > 0).IsTrue();
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Return_Same_Path_On_Multiple_Calls()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var path1 = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var path2 = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            await Assert.That(path1).IsEqualTo(path2);
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Create_Directory_If_It_Does_Not_Exist()
    {
        var testDir = CreateTestDirectory();
        // Delete the directory to test creation
        Directory.Delete(testDir, recursive: true);

        try
        {
            await Assert.That(Directory.Exists(testDir)).IsFalse();

            var templatePath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);

            await Assert.That(Directory.Exists(testDir)).IsTrue();
            await Assert.That(File.Exists(templatePath)).IsTrue();
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Handle_Multiple_Sequential_Calls()
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

            foreach (var path in results)
            {
                await Assert.That(path).IsNotNull();
                await Assert.That(File.Exists(path)).IsTrue();
            }
            await Assert.That(results.TrueForAll(path => string.Equals(path, results[0], StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
    public async Task GetBootstrapTemplatePath_Should_Overwrite_Existing_File()
    {
        var testDir = CreateTestDirectory();
        try
        {
            var firstPath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var originalContent = await File.ReadAllTextAsync(firstPath);

            await File.WriteAllTextAsync(firstPath, "outdated content");

            var secondPath = CdkBootstrapManager.GetBootstrapTemplatePath(testDir);
            var newContent = await File.ReadAllTextAsync(secondPath);

            await Assert.That(firstPath).IsEqualTo(secondPath);
            await Assert.That(originalContent).IsEqualTo(newContent);
            await Assert.That(newContent).IsNotEqualTo("outdated content");
        }
        finally
        {
            CleanupTestDirectory(testDir);
        }
    }

    [Test]
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

            await Assert.That(results.Length).IsEqualTo(parallelTasks);

            foreach (var path in results)
            {
                await Assert.That(string.IsNullOrEmpty(path)).IsFalse();
            }

            var firstPath = results[0];
            foreach (var path in results)
            {
                await Assert.That(path).IsEqualTo(firstPath);
            }

            await Assert.That(File.Exists(firstPath)).IsTrue();
            var fileInfo = new FileInfo(firstPath);
            await Assert.That(fileInfo.Length > 0).IsTrue();
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
