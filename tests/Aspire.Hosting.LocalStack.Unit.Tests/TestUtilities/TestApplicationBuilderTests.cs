namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

public class TestApplicationBuilderTests
{
    [Test]
    public async Task CreateBuilder_Ignores_Ambient_Environment_Variables()
    {
        // A key unique to this test, so setting it cannot affect any test running in parallel.
        var key = "LOCALSTACK_HOSTING_TEST_PROBE_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(key, "ambient");
        try
        {
            var builder = TestApplicationBuilder.CreateBuilder([]);

            await Assert.That(builder.Configuration[key]).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, value: null);
        }
    }

    [Test]
    public async Task CreateBuilder_Keeps_Command_Line_Arguments()
    {
        var builder = TestApplicationBuilder.CreateBuilder(["--Probe:Value=from-args"]);

        await Assert.That(builder.Configuration["Probe:Value"]).IsEqualTo("from-args");
    }
}
