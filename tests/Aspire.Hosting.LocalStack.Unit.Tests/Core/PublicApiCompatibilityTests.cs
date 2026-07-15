namespace Aspire.Hosting.LocalStack.Unit.Tests.Core;

public class PublicApiCompatibilityTests
{
    private static readonly Type BuilderType = typeof(IDistributedApplicationBuilder);
    private static readonly Type StringType = typeof(string);
    private static readonly Type LegacyOptionsType = typeof(ILocalStackOptions);
    private static readonly Type AwsConfigType = typeof(IAWSSDKConfig);
    private static readonly Type ContainerCallbackType = typeof(Action<LocalStackContainerOptions>);
    private static readonly Type HostingCallbackType = typeof(Action<LocalStackHostingOptions>);

    [Test]
    public async Task AddLocalStack_Should_Retain_Released_AllOptional_ClientOptions_Overload_Metadata()
    {
        var method = FindPublicAddLocalStack(
            BuilderType,
            StringType,
            LegacyOptionsType,
            AwsConfigType,
            ContainerCallbackType);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(IResourceBuilder<ILocalStackResource>));

        var parameters = method.GetParameters();
        await Assert.That(parameters[1].Name).IsEqualTo("name");
        await Assert.That(parameters[1].IsOptional).IsTrue();
        await Assert.That(parameters[2].Name).IsEqualTo("localStackOptions");
        await Assert.That(parameters[2].IsOptional).IsTrue();
        await Assert.That(parameters[3].Name).IsEqualTo("awsConfig");
        await Assert.That(parameters[3].IsOptional).IsTrue();
        await Assert.That(parameters[4].Name).IsEqualTo("configureContainer");
        await Assert.That(parameters[4].IsOptional).IsTrue();
        await AssertIsNonErrorObsolete(
            method,
            "Use AddLocalStack(string name, IAWSSDKConfig? awsConfig, Action<LocalStackContainerOptions>? configureContainer) or AddLocalStack(string name, IAWSSDKConfig? awsConfig, Action<LocalStackHostingOptions> configureOptions, Action<LocalStackContainerOptions>? configureContainer = null) instead. This overload will be removed in the next major version.");
    }

    [Test]
    public async Task AddLocalStack_Should_Expose_Approved_PackageOwned_Overload_Metadata()
    {
        await Assert.That(FindPublicAddLocalStack(BuilderType)).IsNotNull();
        await Assert.That(FindPublicAddLocalStack(BuilderType, StringType)).IsNotNull();
        await Assert.That(FindPublicAddLocalStack(BuilderType, StringType, AwsConfigType, ContainerCallbackType)).IsNotNull();

        var explicitCallbackOverload = FindPublicAddLocalStack(
            BuilderType,
            StringType,
            AwsConfigType,
            HostingCallbackType,
            ContainerCallbackType);

        await Assert.That(explicitCallbackOverload).IsNotNull();
        await Assert.That(explicitCallbackOverload!.GetParameters()[4].IsOptional).IsTrue();
    }

    [Test]
    public async Task AddLocalStack_Should_Not_Use_OverloadResolutionPriorityAttribute()
    {
        var overloadPriorityAttributes = PublicAddLocalStackMethods()
            .SelectMany(method => method.GetCustomAttributesData())
            .Where(attribute => string.Equals(
                attribute.AttributeType.FullName,
                "System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute",
                StringComparison.Ordinal))
            .ToArray();

        await Assert.That(overloadPriorityAttributes).IsEmpty();
    }

    [Test]
    public async Task UseLocalStack_Should_Remain_NonObsolete()
    {
        var method = typeof(LocalStackResourceBuilderExtensions).GetMethod(
            nameof(LocalStackResourceBuilderExtensions.UseLocalStack),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(IDistributedApplicationBuilder), typeof(IResourceBuilder<ILocalStackResource>)]);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.GetCustomAttribute<ObsoleteAttribute>()).IsNull();
    }

    [Test]
    public async Task LocalStackResource_Should_Retain_Legacy_Constructor_And_Options_Property_Metadata()
    {
        var constructor = typeof(LocalStackResource).GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            [typeof(string), typeof(ILocalStackOptions)],
            modifiers: null);

#pragma warning disable CS0618
        var optionsProperty = typeof(LocalStackResource).GetProperty(
            nameof(LocalStackResource.Options),
            BindingFlags.Public | BindingFlags.Instance);
#pragma warning restore CS0618

        await Assert.That(constructor).IsNotNull();
        await Assert.That(optionsProperty).IsNotNull();
        await Assert.That(optionsProperty!.PropertyType).IsEqualTo(typeof(ILocalStackOptions));
        await AssertIsNonErrorObsolete(
            constructor!,
            "Use LocalStackResource(string name, LocalStackHostingState hostingState) through AddLocalStack package-owned overloads instead. This constructor will be removed in the next major version.");
        await AssertIsNonErrorObsolete(
            optionsProperty,
            "Use LocalStackResource.HostingState through package-owned runtime adapters instead. This property will be removed in the next major version.");
    }

    [Test]
    public async Task AddLocalStackOptions_Should_Be_NonError_Obsolete()
    {
        var method = typeof(LocalStackResourceBuilderExtensions).GetMethod(
            nameof(LocalStackResourceBuilderExtensions.AddLocalStackOptions),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(IDistributedApplicationBuilder)]);

        await Assert.That(method).IsNotNull();
        await AssertIsNonErrorObsolete(
            method!,
            "Use AddLocalStack package-owned overloads and Aspire:Hosting:LocalStack configuration instead. This method will be removed in the next major version.");
    }

    [Test]
    public async Task LocalStackConfigurationExtensions_Should_Be_NonError_Obsolete()
    {
        var methods = typeof(Aspire.Hosting.LocalStack.Configuration.LocalStackConfigurationExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => Equals(method.DeclaringType, typeof(Aspire.Hosting.LocalStack.Configuration.LocalStackConfigurationExtensions)))
            .ToArray();

        await Assert.That(methods).Count().IsEqualTo(7);

        foreach (var method in methods)
        {
            await AssertIsNonErrorObsolete(
                method,
                "Use LocalStackHostingOptions and LocalStackHostingOptionsExtensions instead. This method will be removed in the next major version.");
        }
    }

    [Test]
    public async Task AwsService_Should_Remain_NonObsolete()
    {
        await Assert.That(typeof(AwsService).GetCustomAttribute<ObsoleteAttribute>()).IsNull();
    }

    private static MethodInfo? FindPublicAddLocalStack(params Type[] parameterTypes)
        => typeof(LocalStackResourceBuilderExtensions).GetMethod(
            nameof(LocalStackResourceBuilderExtensions.AddLocalStack),
            BindingFlags.Public | BindingFlags.Static,
            parameterTypes);

    private static IEnumerable<MethodInfo> PublicAddLocalStackMethods()
        => typeof(LocalStackResourceBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, nameof(LocalStackResourceBuilderExtensions.AddLocalStack), StringComparison.Ordinal));

    private static async Task AssertIsNonErrorObsolete(MemberInfo member, string expectedMessage)
    {
        var obsolete = member.GetCustomAttribute<ObsoleteAttribute>();

        await Assert.That(obsolete).IsNotNull();
        await Assert.That(obsolete!.IsError).IsFalse();
        await Assert.That(obsolete.Message).IsEqualTo(expectedMessage);
    }
}
