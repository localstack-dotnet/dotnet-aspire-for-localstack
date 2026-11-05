using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackAuthTokenResolverTests
{
    [Test]
    public async Task Fluent_Wins_Without_Reading_Lower_Priority_Input()
    {
        var options = new LocalStackContainerOptions();
        options.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-env";
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns(_ =>
            throw new InvalidOperationException("Lower-priority configuration must not be read."));

        var returned = options.WithAuthToken("probe-fluent");
        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(returned).IsSameReferenceAs(options);
        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo("probe-fluent");
        await Assert.That(source.Parameter).IsNull();
        await Assert.That(source.ToString()).DoesNotContain("probe-fluent");
    }

    [Test]
    public async Task ExtraEnv_Wins_Over_Host_Key()
    {
        var options = new LocalStackContainerOptions();
        options.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-env";
        var reads = 0;
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns(_ =>
        {
            reads++;
            return "probe-host";
        });

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo("probe-env");
        await Assert.That(reads).IsEqualTo(0);
    }

    [Test]
    public async Task Host_Key_Is_Used_Only_When_No_Explicit_Source_Exists()
    {
        var options = new LocalStackContainerOptions();
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns("probe-host");

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo("probe-host");
        await Assert.That(source.Parameter).IsNull();
    }

    [Test]
    public async Task Parameter_Is_Retained_Without_Resolution()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var getterCalls = 0;
        var parameter = builder.AddParameter("activation", () =>
        {
            getterCalls++;
            return "probe-parameter";
        }, secret: true);
        var options = new LocalStackContainerOptions();
        options.WithAuthToken(parameter);
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns(_ =>
            throw new InvalidOperationException("Ambient input must not replace an explicit parameter."));

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Parameter).IsSameReferenceAs(parameter);
        await Assert.That(source.Literal).IsNull();
        await Assert.That(source.Kind).IsEqualTo(LocalStackAuthTokenSourceKind.Parameter);
        await Assert.That(getterCalls).IsEqualTo(0);
        await Assert.That(source.ToString()).DoesNotContain("probe-parameter");
    }

    [Test]
    public async Task NonSecret_Parameter_Is_Rejected()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var getterCalls = 0;
        var parameter = builder.AddParameter("activation", () =>
        {
            getterCalls++;
            return "probe-public-value";
        });
        var options = new LocalStackContainerOptions();

        var exception = await Assert.That(() => options.WithAuthToken(parameter))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(getterCalls).IsEqualTo(0);
        await Assert.That(exception!.Message).DoesNotContain("probe-public-value");
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("\t")]
    public async Task Explicit_Blank_Suppresses_Fallback(string blank)
    {
        var options = new LocalStackContainerOptions();
        options.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-env";
        options.WithAuthToken(blank);
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns("probe-host");

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo(blank);
        await Assert.That(source.Parameter).IsNull();
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    public async Task Explicit_ExtraEnv_Blank_Suppresses_Host_Key(string blank)
    {
        var options = new LocalStackContainerOptions();
        options.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = blank;
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns("probe-host");

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo(blank);
    }

    [Test]
    public async Task Repeated_Fluent_Input_Uses_Last_Call_Including_Blank()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        var parameter = builder.AddParameter("activation", () =>
            throw new InvalidOperationException("The earlier source must not be resolved."), secret: true);
        var options = new LocalStackContainerOptions();
        options.WithAuthToken("probe-first").WithAuthToken(parameter).WithAuthToken(" ");
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns("probe-host");

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo(" ");
        await Assert.That(source.Parameter).IsNull();
    }

    [Test]
    public async Task Wrong_Case_ExtraEnv_Key_Does_Not_Replace_Host_Key()
    {
        var options = new LocalStackContainerOptions();
        options.AdditionalEnvironmentVariables["localstack_auth_token"] = "probe-wrong-key";
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns("probe-host");

        var source = LocalStackAuthTokenResolver.Resolve(options, configuration);

        await Assert.That(source).IsNotNull();
        await Assert.That(source!.Literal).IsEqualTo("probe-host");
    }

    [Test]
    public async Task Absent_Input_Does_Not_Create_A_Deferred_Source()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["LOCALSTACK_AUTH_TOKEN"].Returns((string?)null);

        var source = LocalStackAuthTokenResolver.Resolve(new LocalStackContainerOptions(), configuration);

        await Assert.That(source).IsNull();
    }

    [Test]
    public async Task Typed_Null_Token_Inputs_Are_Rejected()
    {
        var options = new LocalStackContainerOptions();

        await Assert.That(() => options.WithAuthToken((string)null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => options.WithAuthToken((IResourceBuilder<ParameterResource>)null!))
            .ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => LocalStackContainerOptionsExtensions.WithAuthToken(null!, "probe"))
            .ThrowsExactly<ArgumentNullException>();
    }
}
