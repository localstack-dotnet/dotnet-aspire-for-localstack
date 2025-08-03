namespace Aspire.Hosting.LocalStack.Unit.Tests.Container;

public class LocalStackLogLevelTests
{
    [Theory]
    [MemberData(nameof(AllLogLevelMappingsTestData))]
    public void ToEnvironmentValue_Should_Map_All_Enum_Values_Correctly(LocalStackLogLevel logLevel, string expectedValue)
    {
        var result = logLevel.ToEnvironmentValue();

        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void LocalStackLogLevel_Should_Have_All_Expected_Values()
    {
        var enumValues = Enum.GetValues<LocalStackLogLevel>();

        Assert.Equal(7, enumValues.Length);
        Assert.Contains(LocalStackLogLevel.Trace, enumValues);
        Assert.Contains(LocalStackLogLevel.TraceInternal, enumValues);
        Assert.Contains(LocalStackLogLevel.Debug, enumValues);
        Assert.Contains(LocalStackLogLevel.Info, enumValues);
        Assert.Contains(LocalStackLogLevel.Warn, enumValues);
        Assert.Contains(LocalStackLogLevel.Error, enumValues);
        Assert.Contains(LocalStackLogLevel.Warning, enumValues);
    }

    [Fact]
    public void LocalStackLogLevel_Should_Have_Unique_Integer_Values()
    {
        var enumValues = Enum.GetValues<LocalStackLogLevel>();
        var integerValues = enumValues.Cast<int>().ToArray();

        Assert.Equal(integerValues.Length, integerValues.Distinct().Count());
    }

    public static IEnumerable<object[]> AllLogLevelMappingsTestData =>
    [
        [LocalStackLogLevel.Trace, "trace"],
        [LocalStackLogLevel.TraceInternal, "trace-internal"],
        [LocalStackLogLevel.Debug, "debug"],
        [LocalStackLogLevel.Info, "info"],
        [LocalStackLogLevel.Warn, "warn"],
        [LocalStackLogLevel.Error, "error"],
        [LocalStackLogLevel.Warning, "warning"]
    ];
}
