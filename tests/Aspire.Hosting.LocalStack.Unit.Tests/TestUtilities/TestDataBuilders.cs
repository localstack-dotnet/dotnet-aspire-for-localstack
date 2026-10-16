namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class TestDataBuilders
{
    public static (ILocalStackOptions, ConfigOptions, SessionOptions) CreateMockLocalStackOptions(
        bool useLocalStack = true,
        string regionName = "us-east-1",
        int edgePort = 4566,
        string localStackHost = "localhost",
        bool useSsl = false,
        bool useLegacyPorts = false,
        string awsAccessKeyId = "test-key",
        string awsAccessKey = "test-secret",
        string awsSessionToken = "test-token")
    {
        var mockOptions = Substitute.For<ILocalStackOptions>();

        // Create concrete instances using available constructors so the legacy
        // ILocalStackOptions surface used by the resolver reflects real values.
        var configOptions = new ConfigOptions(localStackHost, useSsl, useLegacyPorts, edgePort);
        var sessionOptions = new SessionOptions(awsAccessKeyId, awsAccessKey, awsSessionToken, regionName);

        mockOptions.UseLocalStack.Returns(useLocalStack);
        mockOptions.Config.Returns(configOptions);
        mockOptions.Session.Returns(sessionOptions);

        return (mockOptions, configOptions, sessionOptions);
    }

    /// <summary>
    /// Builds a real <see cref="ILocalStackOptions"/> instance (not a substitute) for tests
    /// that need the explicit legacy-options bypass path to consume concrete values.
    /// </summary>
    public static ILocalStackOptions CreateExplicitLocalStackOptions(
        bool useLocalStack = true,
        string regionName = "us-east-1",
        string awsAccessKeyId = "explicit-key",
        string awsAccessKey = "explicit-secret",
        string awsSessionToken = "explicit-token",
        string localStackHost = "localhost",
        int edgePort = 4566,
        bool useSsl = false,
        bool useLegacyPorts = false)
    {
        var sessionOptions = new SessionOptions(awsAccessKeyId, awsAccessKey, awsSessionToken, regionName);
        var configOptions = new ConfigOptions(localStackHost, useSsl, useLegacyPorts, edgePort);

        return new LocalStackOptions(useLocalStack, sessionOptions, configOptions);
    }

    public static IAWSSDKConfig CreateMockAWSConfig(string regionName = "us-west-2")
    {
        var mockConfig = Substitute.For<IAWSSDKConfig>();
        mockConfig.Region.Returns(Amazon.RegionEndpoint.GetBySystemName(regionName));
        return mockConfig;
    }

    public static LocalStackHostingState CreateHostingState(
        bool enabled = true,
        string regionName = "us-east-1",
        bool useSsl = false,
        bool useLegacyPorts = false,
        string awsAccessKeyId = "test-key",
        string awsAccessKey = "test-secret",
        string awsSessionToken = "test-token")
        => new()
        {
            Enabled = enabled,
            Region = regionName,
            AccessKeyId = awsAccessKeyId,
            SecretAccessKey = awsAccessKey,
            SessionToken = awsSessionToken,
            UseSsl = useSsl,
            UseLegacyPorts = useLegacyPorts,
        };

    /// <summary>
    /// Gets a real project file path for testing project functionality.
    /// Uses the test project itself as a valid project file.
    /// </summary>
    public static string GetTestProjectPath([CallerFilePath] string callerFile = "")
    {
        ArgumentNullException.ThrowIfNull(callerFile);

        var dir = Path.GetDirectoryName(callerFile)!;

        while (dir is not null)
        {
            var hit = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (hit is not null)
            {
                return Path.GetFullPath(hit);
            }

            dir = Path.GetDirectoryName(dir); // climb one level
        }

        throw new FileNotFoundException($"Could not locate a *.csproj file above {callerFile}");
    }
}
