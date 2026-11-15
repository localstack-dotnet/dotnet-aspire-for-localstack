namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

internal static class IntegrationTestSession
{
    private static DirectoryInfo? _temporaryDirectory;
    private static (string Name, string? Value)[] _previousTempVariables = [];
    private static (string Name, string? Value)[] _previousCertificateVariables = [];

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

        // Aspire trusts and serves the ASP.NET Core development certificate by default. On macOS its
        // private key lives in the login keychain, which refuses a process without a desktop session
        // (SSH, CI agents) instead of prompting. These tests use no HTTPS endpoint, so they opt out.
        _previousCertificateVariables =
        [
            ("ASPIRE_DEVELOPER_CERTIFICATE_DEFAULT_TRUST", Environment.GetEnvironmentVariable("ASPIRE_DEVELOPER_CERTIFICATE_DEFAULT_TRUST")),
            ("ASPIRE_DEVELOPER_CERTIFICATE_DEFAULT_HTTPS_TERMINATION", Environment.GetEnvironmentVariable("ASPIRE_DEVELOPER_CERTIFICATE_DEFAULT_HTTPS_TERMINATION")),
        ];
        foreach (var (name, _) in _previousCertificateVariables)
        {
            Environment.SetEnvironmentVariable(name, "false");
        }
    }

    [After(TestSession)]
    public static void RemoveTemporaryDirectory()
    {
        foreach (var (name, value) in _previousTempVariables.Concat(_previousCertificateVariables))
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        // Session-shared AppHosts have been disposed before this hook runs.
        // Their running Lambda processes still need these projects until then.
        _temporaryDirectory?.Delete(recursive: true);
        _temporaryDirectory = null;
    }
}
