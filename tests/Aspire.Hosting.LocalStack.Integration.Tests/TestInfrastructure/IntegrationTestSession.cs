namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

internal static class IntegrationTestSession
{
    private static DirectoryInfo? _temporaryDirectory;
    private static (string Name, string? Value)[] _previousTempVariables = [];

    [Before(TestSession)]
    public static void CreateTemporaryDirectory()
    {
        _temporaryDirectory = Directory.CreateTempSubdirectory("localstack-tests-");
        _previousTempVariables =
        [
            ("TMPDIR", Environment.GetEnvironmentVariable("TMPDIR")),
            ("TEMP", Environment.GetEnvironmentVariable("TEMP")),
            ("TMP", Environment.GetEnvironmentVariable("TMP")),
        ];

        // AWS hosting creates Lambda wrapper projects under the process temp path.
        // Keep them outside the checkout so they do not inherit its MSBuild policy.
        foreach (var (name, _) in _previousTempVariables)
        {
            Environment.SetEnvironmentVariable(name, _temporaryDirectory.FullName);
        }
    }

    [After(TestSession)]
    public static void RemoveTemporaryDirectory()
    {
        foreach (var (name, value) in _previousTempVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        // Session-shared AppHosts have been disposed before this hook runs.
        // Their running Lambda processes still need these projects until then.
        _temporaryDirectory?.Delete(recursive: true);
        _temporaryDirectory = null;
    }
}
