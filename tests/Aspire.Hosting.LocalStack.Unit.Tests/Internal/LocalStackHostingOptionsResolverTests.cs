using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackHostingOptionsResolverTests
{
    private const string CanonicalSection = "Aspire:Hosting:LocalStack";
    private const string LegacySection = "LocalStack";

    private static ConfigurationManager EmptyConfiguration()
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection();
        return manager;
    }

    private static ConfigurationManager WithLegacySection(
        bool useLocalStack,
        string region = "legacy-region",
        string awsAccessKeyId = "legacy-key",
        string awsAccessKey = "legacy-secret",
        string awsSessionToken = "legacy-token",
        bool useSsl = false,
        bool useLegacyPorts = false)
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{LegacySection}:UseLocalStack"] = useLocalStack.ToString().ToLowerInvariant(),
            [$"{LegacySection}:Session:RegionName"] = region,
            [$"{LegacySection}:Session:AwsAccessKeyId"] = awsAccessKeyId,
            [$"{LegacySection}:Session:AwsAccessKey"] = awsAccessKey,
            [$"{LegacySection}:Session:AwsSessionToken"] = awsSessionToken,
            [$"{LegacySection}:Config:UseSsl"] = useSsl.ToString().ToLowerInvariant(),
            [$"{LegacySection}:Config:UseLegacyPorts"] = useLegacyPorts.ToString().ToLowerInvariant(),
        });
        return manager;
    }

    private static ConfigurationManager WithCanonicalSection(
        bool enabled,
        string region = "canonical-region",
        string accessKeyId = "canonical-key",
        string secretAccessKey = "canonical-secret",
        string sessionToken = "canonical-token")
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{CanonicalSection}:Enabled"] = enabled.ToString().ToLowerInvariant(),
            [$"{CanonicalSection}:Region"] = region,
            [$"{CanonicalSection}:AccessKeyId"] = accessKeyId,
            [$"{CanonicalSection}:SecretAccessKey"] = secretAccessKey,
            [$"{CanonicalSection}:SessionToken"] = sessionToken,
        });
        return manager;
    }

    private static ConfigurationManager WithBothSections(
        bool legacyUseLocalStack,
        string legacyRegion,
        string legacyKey,
        string legacySecret,
        string legacyToken,
        bool canonicalEnabled,
        string canonicalRegion,
        string canonicalKey,
        string canonicalSecret,
        string canonicalToken)
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{LegacySection}:UseLocalStack"] = legacyUseLocalStack.ToString().ToLowerInvariant(),
            [$"{LegacySection}:Session:RegionName"] = legacyRegion,
            [$"{LegacySection}:Session:AwsAccessKeyId"] = legacyKey,
            [$"{LegacySection}:Session:AwsAccessKey"] = legacySecret,
            [$"{LegacySection}:Session:AwsSessionToken"] = legacyToken,
            [$"{CanonicalSection}:Enabled"] = canonicalEnabled.ToString().ToLowerInvariant(),
            [$"{CanonicalSection}:Region"] = canonicalRegion,
            [$"{CanonicalSection}:AccessKeyId"] = canonicalKey,
            [$"{CanonicalSection}:SecretAccessKey"] = canonicalSecret,
            [$"{CanonicalSection}:SessionToken"] = canonicalToken,
        });
        return manager;
    }

    // ---- Step 1: precedence tests --------------------------------------------

    [Test]
    public async Task Resolve_NoSection_Returns_NoState_DisabledDefault()
    {
        using var config = EmptyConfiguration();

        var state = LocalStackHostingOptionsResolver.Resolve(config);

        await Assert.That(state).IsNull();
    }

    [Test]
    public async Task Resolve_LegacySection_Maps_OwnedValues_Into_State()
    {
        using var config = WithLegacySection(
            useLocalStack: true,
            region: "legacy-region",
            awsAccessKeyId: "legacy-key",
            awsAccessKey: "legacy-secret",
            awsSessionToken: "legacy-token",
            useSsl: true,
            useLegacyPorts: true);

        var state = LocalStackHostingOptionsResolver.Resolve(config);

        await Assert.That(state).IsNotNull();
        await Assert.That(state!.Enabled).IsTrue();
        await Assert.That(state.Region).IsEqualTo("legacy-region");
        await Assert.That(state.AccessKeyId).IsEqualTo("legacy-key");
        await Assert.That(state.SecretAccessKey).IsEqualTo("legacy-secret");
        await Assert.That(state.SessionToken).IsEqualTo("legacy-token");
        await Assert.That(state.UseSsl).IsTrue();
        await Assert.That(state.UseLegacyPorts).IsTrue();
    }

    [Test]
    public async Task Resolve_CanonicalSection_Overrides_EquivalentLegacyValues()
    {
        using var config = WithBothSections(
            legacyUseLocalStack: true,
            legacyRegion: "legacy-region",
            legacyKey: "legacy-key",
            legacySecret: "legacy-secret",
            legacyToken: "legacy-token",
            canonicalEnabled: true,
            canonicalRegion: "canonical-region",
            canonicalKey: "canonical-key",
            canonicalSecret: "canonical-secret",
            canonicalToken: "canonical-token");

        var state = LocalStackHostingOptionsResolver.Resolve(config);

        await Assert.That(state).IsNotNull();
        await Assert.That(state!.Region).IsEqualTo("canonical-region");
        await Assert.That(state.AccessKeyId).IsEqualTo("canonical-key");
        await Assert.That(state.SecretAccessKey).IsEqualTo("canonical-secret");
        await Assert.That(state.SessionToken).IsEqualTo("canonical-token");
    }

    [Test]
    public async Task Resolve_ConfigureOptions_Overrides_CanonicalValues()
    {
        using var config = WithCanonicalSection(
            enabled: true,
            region: "canonical-region",
            accessKeyId: "canonical-key",
            secretAccessKey: "canonical-secret",
            sessionToken: "canonical-token");

        var state = LocalStackHostingOptionsResolver.Resolve(
            config,
            configureOptions: o =>
            {
                o.Region = "callback-region";
                o.AccessKeyId = "callback-key";
                o.SecretAccessKey = "callback-secret";
                o.SessionToken = "callback-token";
            });

        await Assert.That(state).IsNotNull();
        await Assert.That(state!.Region).IsEqualTo("callback-region");
        await Assert.That(state.AccessKeyId).IsEqualTo("callback-key");
        await Assert.That(state.SecretAccessKey).IsEqualTo("callback-secret");
        await Assert.That(state.SessionToken).IsEqualTo("callback-token");
    }

    [Test]
    public async Task Resolve_AwsSdkConfigRegion_Overrides_CallbackRegion()
    {
        using var config = WithCanonicalSection(enabled: true, region: "canonical-region");

        var awsConfig = TestDataBuilders.CreateMockAWSConfig("us-west-2");

        var state = LocalStackHostingOptionsResolver.Resolve(
            config,
            awsConfig: awsConfig,
            configureOptions: o => o.Region = "callback-region");

        await Assert.That(state).IsNotNull();
        await Assert.That(state!.Region).IsEqualTo("us-west-2");
    }

    [Test]
    public async Task Resolve_ExplicitLocalStackOptions_Bypasses_SectionBinding_But_AppliesAwsSdkConfigRegion()
    {
        using var config = WithBothSections(
            legacyUseLocalStack: false,
            legacyRegion: "legacy-region",
            legacyKey: "legacy-key",
            legacySecret: "legacy-secret",
            legacyToken: "legacy-token",
            canonicalEnabled: false,
            canonicalRegion: "canonical-region",
            canonicalKey: "canonical-key",
            canonicalSecret: "canonical-secret",
            canonicalToken: "canonical-token");

        var explicitOptions = TestDataBuilders.CreateExplicitLocalStackOptions(
            useLocalStack: true,
            regionName: "explicit-region",
            awsAccessKeyId: "explicit-key",
            awsAccessKey: "explicit-secret",
            awsSessionToken: "explicit-token");

        var awsConfig = TestDataBuilders.CreateMockAWSConfig("eu-central-1");

        var state = LocalStackHostingOptionsResolver.Resolve(
            config,
            awsConfig: awsConfig,
            localStackOptions: explicitOptions);

        await Assert.That(state).IsNotNull();
        await Assert.That(state!.Enabled).IsTrue();
        await Assert.That(state.Region).IsEqualTo("eu-central-1");
        await Assert.That(state.AccessKeyId).IsEqualTo("explicit-key");
        await Assert.That(state.SecretAccessKey).IsEqualTo("explicit-secret");
        await Assert.That(state.SessionToken).IsEqualTo("explicit-token");
    }

    [Test]
    public async Task Resolve_ExplicitDisabledLocalStackOptions_Returns_NoState_EvenWhenSectionsEnable()
    {
        using var config = WithCanonicalSection(enabled: true);

        var explicitDisabled = TestDataBuilders.CreateExplicitLocalStackOptions(useLocalStack: false);

        var state = LocalStackHostingOptionsResolver.Resolve(config, localStackOptions: explicitDisabled);

        await Assert.That(state).IsNull();
    }

    // ---- Step 2: validation tests --------------------------------------------

    [Test]
    [Arguments("Region")]
    [Arguments("AccessKeyId")]
    [Arguments("SecretAccessKey")]
    [Arguments("SessionToken")]
    public async Task Resolve_Enabled_WithInvalidProperty_ThrowsDistributedApplicationException_NamingPropertyWithoutCredentialValues(
        string property)
    {
        const string secretMarker = "REAL-SECRET-VALUE-MUST-NOT-LEAK-123";

        using var config = EmptyConfiguration();

        var exception = await Assert.That(() => LocalStackHostingOptionsResolver.Resolve(
            config,
            configureOptions: o =>
            {
                o.Enabled = true;
                o.AccessKeyId = "id";
                o.SecretAccessKey = secretMarker;
                o.SessionToken = "token";
                switch (property)
                {
                    case "Region":
                        o.Region = "";
                        break;
                    case "AccessKeyId":
                        o.AccessKeyId = " ";
                        break;
                    case "SecretAccessKey":
                        o.SecretAccessKey = "\t";
                        break;
                    case "SessionToken":
                        o.SessionToken = "";
                        break;
                }
            })).ThrowsExactly<DistributedApplicationException>();

        await Assert.That(exception!.Message).Contains(property);
        await Assert.That(exception.Message).DoesNotContain(secretMarker);
    }

    [Test]
    public async Task Resolve_Enabled_WithMultipleInvalidProperties_AggregatesAllNames_WithoutExposingCredentialValues()
    {
        const string validSecret = "VALID-SECRET-VALUE-MUST-NOT-LEAK-987";

        using var config = EmptyConfiguration();

        var exception = await Assert.That(() => LocalStackHostingOptionsResolver.Resolve(
            config,
            configureOptions: o =>
            {
                o.Enabled = true;
                o.Region = "";
                o.AccessKeyId = " ";
                o.SecretAccessKey = validSecret;
                o.SessionToken = "\t";
            })).ThrowsExactly<DistributedApplicationException>();

        await Assert.That(exception!.Message).Contains("Region");
        await Assert.That(exception.Message).Contains("AccessKeyId");
        await Assert.That(exception.Message).Contains("SessionToken");
        await Assert.That(exception.Message).DoesNotContain("SecretAccessKey");
        await Assert.That(exception.Message).DoesNotContain(validSecret);
    }

    [Test]
    public async Task Resolve_Disabled_WithInvalidValues_ReturnsNoState_WithoutThrowing()
    {
        using var config = EmptyConfiguration();

        var state = LocalStackHostingOptionsResolver.Resolve(
            config,
            configureOptions: o =>
            {
                o.Enabled = false;
                o.Region = "";
                o.AccessKeyId = " ";
                o.SecretAccessKey = "\t";
                o.SessionToken = "";
            });

        await Assert.That(state).IsNull();
    }

    [Test]
    public async Task Resolve_NullConfiguration_ThrowsArgumentNullException()
    {
        IConfiguration configuration = null!;

        await Assert.That(() => LocalStackHostingOptionsResolver.Resolve(configuration))
            .ThrowsExactly<ArgumentNullException>();
    }
}
