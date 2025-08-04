using System.Runtime.CompilerServices;

namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class TestDataBuilders
{
        public static (ILocalStackOptions, ConfigOptions, SessionOptions) CreateMockLocalStackOptions(
        bool useLocalStack = true,
        string regionName = "us-east-1",
        int edgePort = 4566,
        string localStackHost = "localhost",
        bool useSsl = false)
    {
        var mockOptions = Substitute.For<ILocalStackOptions>();

        // Create concrete instances using available constructors
        // For now, use basic constructors and accept default values
        var configOptions = new ConfigOptions(localStackHost, useSsl, useLegacyPorts: false, edgePort);
        var sessionOptions = new SessionOptions("test-key", "test-secret", "test-token", regionName);

        mockOptions.UseLocalStack.Returns(useLocalStack);
        mockOptions.Config.Returns(configOptions);
        mockOptions.Session.Returns(sessionOptions);

        return (mockOptions, configOptions, sessionOptions);
    }

    public static IAWSSDKConfig CreateMockAWSConfig(string regionName = "us-west-2")
    {
        var mockConfig = Substitute.For<IAWSSDKConfig>();
        mockConfig.Region.Returns(Amazon.RegionEndpoint.GetBySystemName(regionName));
        return mockConfig;
    }

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

            dir = Path.GetDirectoryName(dir);   // climb one level
        }

        throw new FileNotFoundException($"Could not locate a *.csproj file above {callerFile}");
    }
}
