using Aspire.Hosting.Eventing;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class LocalStackActivationWiringTests
{
    [Test]
    public async Task Scalar_Becomes_Secret_Without_Published_Default()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);

        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true),
            container => container.WithAuthToken("probe-scalar"));

        await Assert.That(localStack).IsNotNull();
        var parameters = builder.Resources.OfType<ParameterResource>().ToArray();
        await Assert.That(parameters.Length).IsEqualTo(1);
        await Assert.That(parameters[0].Name).IsEqualTo("ls-localstack-auth");
        await Assert.That(parameters[0].Secret).IsTrue();
        await Assert.That(parameters[0].Default).IsNull();
    }

    [Test]
    public async Task Caller_Parameter_Is_Bound_As_The_Original_Deferred_Resource()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var getterCalls = 0;
        var parameter = builder.AddParameter("caller-activation", () =>
        {
            getterCalls++;
            return "probe-caller";
        }, secret: true);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken(parameter));
        await Assert.That(localStack).IsNotNull();

        var context = await EvaluateEnvironment(builder, localStack!.Resource);

        await Assert.That(context.EnvironmentVariables).ContainsKey("LOCALSTACK_AUTH_TOKEN");
        await Assert.That(context.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(parameter.Resource);
        await Assert.That(builder.Resources.OfType<ParameterResource>().Count()).IsEqualTo(1);
        await Assert.That(getterCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Disabled_Does_Not_Read_Token_Or_Create_Parameter()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var trap = new TokenReadTrapProvider();
        builder.Configuration.Sources.Add(new TokenReadTrapSource(trap));
        var containerCalled = false;

        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(false), container =>
            {
                containerCalled = true;
                container.WithAuthToken("probe-disabled");
            });

        await Assert.That(localStack).IsNull();
        await Assert.That(containerCalled).IsFalse();
        await Assert.That(trap.Reads).IsEqualTo(0);
        await Assert.That(builder.Resources.OfType<ParameterResource>()).IsEmpty();
    }

    [Test]
    public async Task Publish_Does_Not_Create_Activation_State()
    {
        var builder = TestApplicationBuilder.CreateBuilder(["--publisher", "manifest"]);
        await Assert.That(builder.ExecutionContext.IsPublishMode).IsTrue();
        var trap = new TokenReadTrapProvider();
        builder.Configuration.Sources.Add(new TokenReadTrapSource(trap));
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container =>
            {
                container.WithAuthToken("probe-publish");
                container.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-extra";
                container.AdditionalEnvironmentVariables["KEEP"] = "probe-keep";
            });
        await Assert.That(localStack).IsNotNull();

        var context = await EvaluateEnvironment(builder, localStack!.Resource);

        await Assert.That(builder.Resources.OfType<ParameterResource>()).IsEmpty();
        await Assert.That(trap.Reads).IsEqualTo(0);
        await Assert.That(context.EnvironmentVariables.ContainsKey("LOCALSTACK_AUTH_TOKEN")).IsFalse();
        await Assert.That(context.EnvironmentVariables["KEEP"]).IsEqualTo("probe-keep");
    }

    [Test]
    public async Task Missing_Current_Binding_Fails_Safely()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true));
        await Assert.That(localStack).IsNotNull();
        await using var app = builder.Build();

        await PublishBeforeStart(app);
        var exception = await Assert.That(async () => await EvaluateEnvironment(builder, localStack!.Resource))
            .ThrowsExactly<DistributedApplicationException>();

        await Assert.That(exception!.Message).Contains("localstack");
        await Assert.That(exception.Message).Contains("LOCALSTACK_AUTH_TOKEN");
    }

    [Test]
    public async Task Guard_Sees_PostAdd_Binding_Without_Reading_Parameter_Value()
    {
        var builder = CreateRunBuilder();
        var getterCalls = 0;
        var parameter = builder.AddParameter("late-activation", () =>
        {
            getterCalls++;
            throw new InvalidOperationException("Deferred activation must not be read by metadata discovery.");
        }, secret: true);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true));
        await Assert.That(localStack).IsNotNull();
        localStack!.WithEnvironment("LOCALSTACK_AUTH_TOKEN", parameter);
        await using var app = builder.Build();

        await PublishBeforeStart(app);
        var context = await EvaluateEnvironment(builder, localStack.Resource);

        await Assert.That(context.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(parameter.Resource);
        await Assert.That(getterCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Effective_Custom_Image_Is_Not_Required_To_Bind_A_Token()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true));
        await Assert.That(localStack).IsNotNull();
        builder.CreateResourceBuilder((LocalStackResource)localStack!.Resource)
            .WithImageRegistry("mirror.example");
        await using var app = builder.Build();

        await PublishBeforeStart(app);
        var context = await EvaluateEnvironment(builder, localStack.Resource);

        await Assert.That(context.EnvironmentVariables.ContainsKey("LOCALSTACK_AUTH_TOKEN")).IsFalse();
    }

    [Test]
    public async Task Legacy_Preset_Configures_The_Qualified_Image_Without_Requiring_Activation()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithLegacy());
        await Assert.That(localStack).IsNotNull();
        var image = localStack!.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        await Assert.That(image.Tag).IsEqualTo("4.14.0");
        await using var app = builder.Build();

        await PublishBeforeStart(app);
        var context = await EvaluateEnvironment(builder, localStack.Resource);

        await Assert.That(context.EnvironmentVariables.ContainsKey("LOCALSTACK_AUTH_TOKEN")).IsFalse();
        await Assert.That(builder.Resources.OfType<ParameterResource>()).IsEmpty();
    }

    [Test]
    public async Task Cached_Discovery_Does_Not_Lose_Activation_Binding()
    {
        var builder = CreateRunBuilder();
        var getterCalls = 0;
        var parameter = builder.AddParameter("activation", () =>
        {
            getterCalls++;
            throw new InvalidOperationException("Dependency discovery must not resolve a parameter value.");
        }, secret: true);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken(parameter));
        await Assert.That(localStack).IsNotNull();
        var cached = new ResourceDependencyDiscoveryOptions { CacheAnnotationCallbackResults = true };
        await using var app = builder.Build();

        var before = await localStack!.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached);
        await PublishBeforeStart(app);
        var after = await localStack.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached);
        var uncached = await localStack.Resource.GetResourceDependenciesAsync(builder.ExecutionContext);

        await Assert.That(before).Contains(parameter.Resource);
        await Assert.That(after).Contains(parameter.Resource);
        await Assert.That(uncached).Contains(parameter.Resource);
        await Assert.That(getterCalls).IsEqualTo(0);
    }

    [Test]
    [Arguments(false, null)]
    [Arguments(true, "")]
    [Arguments(true, " ")]
    [Arguments(true, null)]
    public async Task Cached_Missing_Or_Literal_Blank_Current_Binding_Still_Fails(bool addBinding, string? value)
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true));
        await Assert.That(localStack).IsNotNull();
        if (addBinding)
        {
            localStack!.WithEnvironment("LOCALSTACK_AUTH_TOKEN", value);
        }

        var cached = new ResourceDependencyDiscoveryOptions { CacheAnnotationCallbackResults = true };
        await using var app = builder.Build();
        await localStack!.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached);
        await PublishBeforeStart(app);

        await Assert.That(async () => await localStack.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached))
            .ThrowsExactly<DistributedApplicationException>();
    }

    [Test]
    public async Task Cached_Late_Blank_Does_Not_Fall_Back_To_Earlier_Private_Parameter()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken("probe-earlier"));
        await Assert.That(localStack).IsNotNull();
        localStack!.WithEnvironment("LOCALSTACK_AUTH_TOKEN", " ");
        var cached = new ResourceDependencyDiscoveryOptions { CacheAnnotationCallbackResults = true };
        await using var app = builder.Build();
        await localStack.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached);
        await PublishBeforeStart(app);

        var exception = await Assert.That(async () => await localStack.Resource.GetResourceDependenciesAsync(builder.ExecutionContext, cached))
            .ThrowsExactly<DistributedApplicationException>();

        await Assert.That(exception!.Message).DoesNotContain("probe-earlier");
    }

    [Test]
    public async Task Repeated_Registration_Is_Idempotent()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken("probe-once"));
        await Assert.That(localStack).IsNotNull();
        var resource = (LocalStackResource)localStack!.Resource;
        var concrete = builder.CreateResourceBuilder(resource);
        var originalParameter = builder.Resources.OfType<ParameterResource>().Single();
        var callbacksBefore = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count();
        LocalStackActivationSetup.RegisterGuard(builder, concrete, LocalStackRuntimeCatalog.Production);
        LocalStackActivationSetup.RegisterGuard(builder, concrete, LocalStackRuntimeCatalog.Production);
        var repeated = LocalStackActivationSetup.ApplyToken(builder, concrete, LocalStackAuthTokenSource.FromLiteral("probe-not-applied"));
        await using var app = builder.Build();

        await PublishBeforeStart(app);
        await PublishBeforeStart(app);
        var context = await EvaluateEnvironment(builder, resource);

        await Assert.That(repeated).IsSameReferenceAs(originalParameter);
        await Assert.That(builder.Resources.OfType<ParameterResource>().Count()).IsEqualTo(1);
        await Assert.That(resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count()).IsEqualTo(callbacksBefore + 1);
        await Assert.That(context.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(originalParameter);
    }

    [Test]
    public async Task Private_Parameter_Name_Is_Bounded_And_Does_Not_Reuse_Caller_Collision()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        var resourceName = "localstack-" + new string('x', 50);
        var baseName = "ls-" + resourceName[..40] + "-auth";
        var caller = builder.AddParameter(baseName.ToUpperInvariant(), () => "probe-caller", secret: true);

        builder.AddLocalStack(resourceName, awsConfig: null, options => options.WithEnabled(true),
            container => container.WithAuthToken("probe-private"));

        var parameters = builder.Resources.OfType<ParameterResource>().ToArray();
        await Assert.That(parameters.Length).IsEqualTo(2);
        var generated = parameters.Single(parameter => !ReferenceEquals(parameter, caller.Resource));
        await Assert.That(generated.Name).IsEqualTo(baseName + "-1");
        await Assert.That(generated.Name.Length).IsLessThan(65);
        await Assert.That(generated.Secret).IsTrue();
        await Assert.That(caller.Resource.Secret).IsTrue();
    }

    [Test]
    public async Task Explicit_Parameter_Failure_Does_Not_Fallback()
    {
        var builder = CreateRunBuilder();
        var trap = new TokenReadTrapProvider();
        builder.Configuration.Sources.Add(new TokenReadTrapSource(trap));
        var getterCalls = 0;
        var parameter = builder.AddParameter("activation", () =>
        {
            getterCalls++;
            throw new InvalidOperationException("Explicit activation provider failed.");
        }, secret: true);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken(parameter));
        await Assert.That(localStack).IsNotNull();
        await using var app = builder.Build();
        await PublishBeforeStart(app);
        await localStack!.Resource.GetResourceDependenciesAsync(builder.ExecutionContext);
        await Assert.That(getterCalls).IsEqualTo(0);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.That(async () => await parameter.Resource.GetValueAsync(cancellation.Token))
            .ThrowsExactly<InvalidOperationException>();
        var context = await EvaluateEnvironment(builder, localStack.Resource);

        await Assert.That(context.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(parameter.Resource);
        await Assert.That(getterCalls).IsEqualTo(1);
        await Assert.That(trap.Reads).IsEqualTo(0);
    }

    [Test]
    public async Task Guard_Does_Not_Await_Deferred_Parameter_And_Core_Cancellation_Is_Preserved()
    {
        var builder = CreateRunBuilder();
        var getterCalls = 0;
        var parameter = builder.AddParameter("activation", () =>
        {
            getterCalls++;
            return "probe-waiting";
        }, secret: true);
        // Characterize Core's pre-initialization wait state without starting its host services.
        var waitProperty = typeof(ParameterResource).GetProperty("WaitForValueTcs", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Core parameter initialization wait state was not found.");
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        waitProperty.SetValue(parameter.Resource, pending);
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithAuthToken(parameter));
        await Assert.That(localStack).IsNotNull();
        await using var app = builder.Build();
        await PublishBeforeStart(app);
        using var discoveryCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var dependencies = await localStack!.Resource.GetResourceDependenciesAsync(builder.ExecutionContext,
            new ResourceDependencyDiscoveryOptions { CacheAnnotationCallbackResults = true }, discoveryCancellation.Token);
        using var valueCancellation = new CancellationTokenSource();
        await valueCancellation.CancelAsync();
        await Assert.That(async () => await parameter.Resource.GetValueAsync(valueCancellation.Token))
            .Throws<OperationCanceledException>();

        await Assert.That(dependencies).Contains(parameter.Resource);
        await Assert.That(getterCalls).IsEqualTo(0);
        await Assert.That(pending.Task.IsCompleted).IsFalse();
    }

    [Test]
    [Arguments("fluent")]
    [Arguments("extra")]
    [Arguments("host")]
    public async Task Scalar_Sources_Stay_Secret_And_Separate_From_Aws_And_Workload_State(string source)
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        builder.Configuration["LOCALSTACK_AUTH_TOKEN"] = "probe-host";
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true).WithCredentials("aws-id", "aws-secret", "aws-session"),
            container =>
            {
                if (source == "fluent")
                {
                    container.WithAuthToken("probe-fluent");
                }
                else if (source == "extra")
                {
                    container.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-extra";
                }
            });
        await Assert.That(localStack).IsNotNull();
        var worker = builder.AddResource(new ExecutableResource("worker", "not-run", "."))
            .WithReference(localStack);

        var containerEnvironment = await EvaluateEnvironment(builder, localStack!.Resource);
        var workerEnvironment = await EvaluateEnvironment(builder, worker.Resource);
        var parameter = builder.Resources.OfType<ParameterResource>().Single();
        var state = localStack.Resource.GetHostingState();

        await Assert.That(containerEnvironment.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(parameter);
        await Assert.That(parameter.Secret).IsTrue();
        await Assert.That(parameter.Default).IsNull();
        await Assert.That(workerEnvironment.EnvironmentVariables).ContainsKey("ConnectionStrings__localstack");
        await Assert.That(workerEnvironment.EnvironmentVariables.ContainsKey("LOCALSTACK_AUTH_TOKEN")).IsFalse();
        await Assert.That(state.AccessKeyId).IsEqualTo("aws-id");
        await Assert.That(state.SecretAccessKey).IsEqualTo("aws-secret");
        await Assert.That(state.SessionToken).IsEqualTo("aws-session");
        await Assert.That(localStack.Resource.ConnectionStringExpression.ValueExpression).DoesNotContain("probe-");
    }

    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("\t")]
    public async Task Blank_Explicit_Input_Does_Not_Create_An_Automatic_Parameter_Or_Fallback(string blank)
    {
        var builder = CreateRunBuilder();
        builder.Configuration["LOCALSTACK_AUTH_TOKEN"] = "probe-host";
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container =>
            {
                container.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "probe-extra";
                container.WithAuthToken(blank);
            });
        await Assert.That(localStack).IsNotNull();
        await Assert.That(builder.Resources.OfType<ParameterResource>()).IsEmpty();
        await using var app = builder.Build();
        await PublishBeforeStart(app);

        var exception = await Assert.That(async () => await EvaluateEnvironment(builder, localStack!.Resource))
            .ThrowsExactly<DistributedApplicationException>();

        await Assert.That(exception!.Message).DoesNotContain("probe-host");
        await Assert.That(exception.Message).DoesNotContain("probe-extra");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Legacy_And_Custom_Images_Do_Not_Strip_Provided_Activation(bool legacy)
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container =>
            {
                container.WithAuthToken("probe-provided");
                if (legacy)
                {
                    container.WithLegacy();
                }
                else
                {
                    container.ContainerImageTag = "custom-build";
                }
            });
        await Assert.That(localStack).IsNotNull();
        var parameter = builder.Resources.OfType<ParameterResource>().Single();
        await using var app = builder.Build();
        await PublishBeforeStart(app);

        var context = await EvaluateEnvironment(builder, localStack!.Resource);

        await Assert.That(context.EnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]).IsSameReferenceAs(parameter);
        await Assert.That(parameter.Secret).IsTrue();
    }

    [Test]
    public async Task Effective_Current_Image_Requires_Activation_Even_After_Legacy_Preset()
    {
        var builder = CreateRunBuilder();
        var localStack = builder.AddLocalStack("localstack", awsConfig: null,
            options => options.WithEnabled(true), container => container.WithLegacy());
        await Assert.That(localStack).IsNotNull();
        builder.CreateResourceBuilder((LocalStackResource)localStack!.Resource).WithImageTag("2026.10.0");
        await using var app = builder.Build();
        await PublishBeforeStart(app);

        await Assert.That(async () => await EvaluateEnvironment(builder, localStack.Resource))
            .ThrowsExactly<DistributedApplicationException>();
    }

    private static IDistributedApplicationBuilder CreateRunBuilder()
    {
        var builder = TestApplicationBuilder.CreateBuilder([]);
        // The model-only event needs DCP option metadata, but never launches either executable.
        builder.Configuration["DcpPublisher:CliPath"] = "model-only-dcp";
        builder.Configuration["DcpPublisher:DashboardPath"] = "model-only-dashboard";
        return builder;
    }

    private static Task PublishBeforeStart(DistributedApplication app)
    {
        var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return eventing.PublishAsync(new BeforeStartEvent(app.Services, model), CancellationToken.None);
    }

    private static async Task<EnvironmentCallbackContext> EvaluateEnvironment(
        IDistributedApplicationBuilder builder, IResource resource)
    {
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        return context;
    }

    private sealed class TokenReadTrapSource(TokenReadTrapProvider provider) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return provider;
        }
    }

    private sealed class TokenReadTrapProvider : ConfigurationProvider
    {
        internal int Reads { get; private set; }

        public override bool TryGet(string key, out string? value)
        {
            if (string.Equals(key, "LOCALSTACK_AUTH_TOKEN", StringComparison.Ordinal))
            {
                Reads++;
                throw new InvalidOperationException("Activation configuration must not be read in this mode.");
            }

            return base.TryGet(key, out value);
        }
    }
}
