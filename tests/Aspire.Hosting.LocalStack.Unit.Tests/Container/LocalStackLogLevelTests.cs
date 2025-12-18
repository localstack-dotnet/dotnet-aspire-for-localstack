namespace Aspire.Hosting.LocalStack.Unit.Tests.Container;

public class LocalStackLogLevelTests
{
    [Test]
    [MethodDataSource(nameof(AllLogLevelMappingsTestData))]
    public async Task ToEnvironmentValue_Should_Map_All_Enum_Values_Correctly(LocalStackLogLevel logLevel, string expectedValue)
    {
        var result = logLevel.ToEnvironmentValue();

        await Assert.That(result).IsEqualTo(expectedValue);
    }

    [Test]
    public async Task LocalStackLogLevel_Should_Have_All_Expected_Values()
    {
        var enumValues = Enum.GetValues<LocalStackLogLevel>();

        await Assert.That(enumValues.Length).IsEqualTo(7);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Trace);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.TraceInternal);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Debug);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Info);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Warn);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Error);
        await Assert.That(enumValues).Contains(LocalStackLogLevel.Warning);
    }

    [Test]
    public async Task LocalStackLogLevel_Should_Have_Unique_Integer_Values()
    {
        var enumValues = Enum.GetValues<LocalStackLogLevel>();
        var integerValues = enumValues.Cast<int>().ToArray();

        await Assert.That(integerValues.Length).IsEqualTo(integerValues.Distinct().Count());
    }

    [Test]
    public async Task ToEnvironmentValue_Should_Handle_Invalid_Enum_Values_Gracefully()
    {
        const LocalStackLogLevel invalidLogLevel = (LocalStackLogLevel)999;

        // The implementation doesn't throw for invalid values, it handles them gracefully
        var result = invalidLogLevel.ToEnvironmentValue();
        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task ToEnvironmentValue_Should_Handle_Negative_Enum_Values_Gracefully()
    {
        const LocalStackLogLevel invalidLogLevel = (LocalStackLogLevel)(-1);

        // The implementation doesn't throw for invalid values, it handles them gracefully
        var result = invalidLogLevel.ToEnvironmentValue();
        await Assert.That(result).IsNotNull();
    }

    public static IEnumerable<(LocalStackLogLevel, string)> AllLogLevelMappingsTestData()
    {
        yield return (LocalStackLogLevel.Trace, "trace");
        yield return (LocalStackLogLevel.TraceInternal, "trace-internal");
        yield return (LocalStackLogLevel.Debug, "debug");
        yield return (LocalStackLogLevel.Info, "info");
        yield return (LocalStackLogLevel.Warn, "warn");
        yield return (LocalStackLogLevel.Error, "error");
        yield return (LocalStackLogLevel.Warning, "warning");
    }
}
