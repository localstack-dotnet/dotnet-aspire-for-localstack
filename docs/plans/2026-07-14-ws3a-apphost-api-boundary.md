# WS3A AppHost API Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace LocalStack.Client-owned AppHost configuration with a package-owned hosting interface while preserving 13.x source, binary, workload, and real-AWS fallback behavior.

**Architecture:** `LocalStackHostingOptions` is a mutable binding/callback DTO. A resolver applies legacy configuration, canonical configuration, callback overrides, and `IAWSSDKConfig.Region`, validates the result, then freezes it into an internal `LocalStackHostingState` stored by `LocalStackResource`. Runtime code reads only that state and translates to LocalStack.Client models at the internal `SessionStandalone` seam. Released Client-owned members remain as obsolete compatibility adapters until the next major version.

**Tech Stack:** C# with .NET 8/9/10, Aspire.Hosting 13.4.6, Aspire.Hosting.AWS 13.3.1, LocalStack.Client 2.0.0, AWSSDK.Core 4.0.9.6, TUnit 1.56.18, Microsoft.Testing.Platform.

## Global Constraints

- Keep `LocalStack.Client` as an internal runtime dependency; do not remove its package reference in WS3A.
- Preserve `LocalStack__*` workload environment names and values for LocalStack.Client.Extensions consumers.
- Do not emit, remove, or overwrite `AWS_ENDPOINT_URL*` automatically.
- Preserve disabled behavior: `AddLocalStack` returns `null`, `UseLocalStack(null)` is a no-op, and real AWS remains selected.
- Preserve the explicit `UseLocalStack(localStack)` selector; do not add an argumentless overload.
- Preserve all released 13.4.0 public metadata through 13.x; obsolete members are removed only in the next major version.
- Keep `LocalStack.Client.Enums.AwsService` as the documented public exception.
- Use pinned upstream refs: Aspire `v13.4.6` at `87fe259e4fc244c599019a7b1304c85a1488f248`, AWS integration `release_2026-06-19` at `b8f5c040b2a41a655ab9ae4d2c528803d5066ae6`, and LocalStack.Client `v2.0.0` at `873efbfbdfb43abe89df2b2e6458dbf90c0a6a92`.
- Do not use `OverloadResolutionPriorityAttribute`; consumers using a pre-C# 13 compiler must compile.
- Do not add a Roslyn analyzer for the legacy optional parameter.
- Do not change target-aware AWS helper attachment or app-like resource auto-wiring; those unresolved behaviors belong to research-first WS3B.
- Change the roadmap status from `📐 Planned` to `🔨 In progress` when production implementation starts; mark WS3A done only after Task 7 verification and final review complete.
- Do not commit without presenting a change summary and proposed Conventional Commit message and receiving explicit approval.

---

## Approved Public Interface

```csharp
namespace Aspire.Hosting.LocalStack;

public sealed class LocalStackHostingOptions
{
    public bool Enabled { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string AccessKeyId { get; set; } = "accessKey";
    public string SecretAccessKey { get; set; } = "secretKey";
    public string SessionToken { get; set; } = "token";
}
```

```csharp
namespace Aspire.Hosting;

public static class LocalStackHostingOptionsExtensions
{
    public static LocalStackHostingOptions WithEnabled(
        this LocalStackHostingOptions options,
        bool enabled);

    public static LocalStackHostingOptions WithRegion(
        this LocalStackHostingOptions options,
        string region);

    public static LocalStackHostingOptions WithCredentials(
        this LocalStackHostingOptions options,
        string accessKeyId,
        string secretAccessKey,
        string sessionToken);
}
```

```csharp
public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder);

public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder,
    string name);

public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder,
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackContainerOptions>? configureContainer);

public static IResourceBuilder<ILocalStackResource>? AddLocalStack(
    this IDistributedApplicationBuilder builder,
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackHostingOptions> configureOptions,
    Action<LocalStackContainerOptions>? configureContainer = null);
```

The released all-optional Client-options overload remains with `[Obsolete]`. All new overloads delegate to one implementation.

Existing calls that bind the released optional overload, including named `awsConfig:` or `configureContainer:` calls that omit `name`, continue to compile but receive CS0618. Repository call sites migrate to an exact package-owned overload. External warnings-as-errors consumers must make the same source migration; binary consumers continue to resolve the retained released signature.

## Configuration Contract

Canonical section:

```text
Aspire:Hosting:LocalStack
```

Environment-variable examples:

```text
Aspire__Hosting__LocalStack__Enabled
Aspire__Hosting__LocalStack__Region
Aspire__Hosting__LocalStack__AccessKeyId
Aspire__Hosting__LocalStack__SecretAccessKey
Aspire__Hosting__LocalStack__SessionToken
```

Precedence, lowest to highest:

1. `LocalStackHostingOptions` defaults.
2. Legacy `LocalStack:*` configuration translated to owned names.
3. Canonical `Aspire:Hosting:LocalStack:*` configuration, including normal .NET provider precedence.
4. The explicit `configureOptions` callback and its fluent methods.
5. `IAWSSDKConfig.Region`, preserving released behavior.

An explicitly supplied legacy `ILocalStackOptions` instance bypasses configuration binding, is translated to internal state, and still receives the `IAWSSDKConfig.Region` override.

## Validation Contract

- Validate after all precedence layers have been applied.
- If `Enabled` is false, return `null` without validating unused region or credential fields.
- If `Enabled` is true, reject null, empty, or whitespace `Region`, `AccessKeyId`, `SecretAccessKey`, and `SessionToken`.
- Do not whitelist region names against the pinned AWS SDK.
- Aggregate invalid property names into one `DistributedApplicationException` without including credential values.

---

### Task 1: Package-Owned Options And Fluent Interface

**Files:**
- Create: `src/Aspire.Hosting.LocalStack/LocalStackHostingOptions.cs`
- Create: `src/Aspire.Hosting.LocalStack/LocalStackHostingOptionsExtensions.cs`
- Create: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Configuration/LocalStackHostingOptionsTests.cs`

**Interfaces:**
- Consumes: none.
- Produces: `LocalStackHostingOptions`, `WithEnabled`, `WithRegion`, and `WithCredentials` exactly as listed in Approved Public Interface.

- [ ] **Step 1: Add failing default-value and fluent-mutation tests**

```csharp
[Test]
public async Task Defaults_Should_Preserve_LocalStackClient_Defaults()
{
    var options = new LocalStackHostingOptions();

    await Assert.That(options.Enabled).IsFalse();
    await Assert.That(options.Region).IsEqualTo("us-east-1");
    await Assert.That(options.AccessKeyId).IsEqualTo("accessKey");
    await Assert.That(options.SecretAccessKey).IsEqualTo("secretKey");
    await Assert.That(options.SessionToken).IsEqualTo("token");
}

[Test]
public async Task Fluent_Methods_Should_Mutate_And_Return_The_Same_Instance()
{
    var options = new LocalStackHostingOptions();

    var result = options
        .WithEnabled(true)
        .WithRegion("eu-central-1")
        .WithCredentials("id", "secret", "token-value");

    await Assert.That(result).IsSameReferenceAs(options);
    await Assert.That(options.Enabled).IsTrue();
    await Assert.That(options.Region).IsEqualTo("eu-central-1");
    await Assert.That(options.AccessKeyId).IsEqualTo("id");
    await Assert.That(options.SecretAccessKey).IsEqualTo("secret");
    await Assert.That(options.SessionToken).IsEqualTo("token-value");
}
```

- [ ] **Step 2: Run the new tests and verify they fail because the owned types do not exist**

Run:

```bash
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj
```

Expected: compilation fails on missing `LocalStackHostingOptions` and extension methods.

- [ ] **Step 3: Implement the options class and fluent methods**

Use public setters so `ConfigurationBinder` and C# callbacks share one DTO. Each fluent method must null-check `options`; `WithRegion` and `WithCredentials` must reject null or whitespace direct arguments and return the same instance.

- [ ] **Step 4: Run the unit tests and confirm all discovered tests pass**

- [ ] **Step 5: Review checkpoint**

Proposed commit after explicit approval: `feat: add package-owned LocalStack hosting options`

---

### Task 2: Configuration Resolution And Immutable State

**Files:**
- Create: `src/Aspire.Hosting.LocalStack/Internal/LocalStackHostingState.cs`
- Create: `src/Aspire.Hosting.LocalStack/Internal/LocalStackHostingOptionsResolver.cs`
- Create: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackHostingOptionsResolverTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestUtilities/TestDataBuilders.cs`

**Interfaces:**
- Consumes: `LocalStackHostingOptions` from Task 1, `IConfiguration`, optional `IAWSSDKConfig`, and optional legacy `ILocalStackOptions`.
- Produces: immutable internal `LocalStackHostingState` with `Enabled`, `Region`, `AccessKeyId`, `SecretAccessKey`, `SessionToken`, `UseSsl`, and `UseLegacyPorts`.

- [ ] **Step 1: Write precedence tests**

Cover these exact cases:

```text
no section -> disabled defaults
legacy section -> owned value mapping
canonical section -> overrides equivalent legacy values
configureOptions -> overrides canonical values
IAWSSDKConfig.Region -> overrides callback Region
explicit ILocalStackOptions -> bypasses section binding
```

Build test configuration with `ConfigurationManager` and ordered `AddInMemoryCollection` providers; do not mutate process-wide environment variables.

- [ ] **Step 2: Write validation tests**

Use one data-driven test for each invalid enabled property and one test proving disabled invalid values return no state without throwing. Assert the aggregate exception names properties but does not contain supplied credential values.

- [ ] **Step 3: Run the resolver tests and verify they fail**

- [ ] **Step 4: Implement resolution**

The resolver must:

```csharp
internal const string CanonicalSectionName = "Aspire:Hosting:LocalStack";
internal const string LegacySectionName = "LocalStack";
```

For the normal path, construct defaults, translate the legacy section, bind the canonical section over it, invoke `configureOptions`, apply `awsConfig.Region`, validate, and freeze values into `LocalStackHostingState`. Preserve legacy `Config.UseSsl` and `Config.UseLegacyPorts` only in internal compatibility fields; do not add them to `LocalStackHostingOptions`.

- [ ] **Step 5: Run resolver tests and confirm all discovered tests pass**

- [ ] **Step 6: Review checkpoint**

Proposed commit after explicit approval: `feat: resolve LocalStack hosting configuration`

---

### Task 3: AddLocalStack Overloads And Resource State

**Files:**
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResource.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/AddLocalStackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Core/LocalStackResourceTests.cs`
- Create: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Core/PublicApiCompatibilityTests.cs`

**Interfaces:**
- Consumes: resolver and immutable state from Task 2.
- Produces: the four approved package-owned overloads, immutable resource state, and retained compatibility adapters. Task 4 applies deprecation attributes only after internal and repository call sites have migrated.

- [ ] **Step 1: Add compile-guard calls for every approved overload**

The test project must contain direct calls for:

```csharp
builder.AddLocalStack();
builder.AddLocalStack("custom");
builder.AddLocalStack("custom", awsConfig, configureContainer);
builder.AddLocalStack("custom", awsConfig, options => options.WithEnabled(true), configureContainer);
```

These calls guard the exact overload family already proven by the SDK 8/C# 12 compatibility spike against reintroducing CS0121.

- [ ] **Step 2: Add reflection tests for exact metadata preservation**

Assert the released Client-options overload and legacy `LocalStackResource` constructor remain present with their exact parameter types. Assert all four approved package-owned overloads are present and `UseLocalStack` remains non-obsolete. Obsolete metadata is added and asserted in Task 4.

- [ ] **Step 3: Add behavior tests for callback timing and disabled results**

Assert canonical binding runs before `configureOptions`, `configureContainer` is not called when hosting options resolve disabled, and every new overload produces equivalent state when configured equivalently.

- [ ] **Step 4: Run the AddLocalStack and public-interface tests and verify failure**

- [ ] **Step 5: Implement exact forwarding overloads and one private core**

The parameterless and name-only overloads call the normal resolver. The AWS/container overload passes no hosting callback. The explicit overload passes its non-null hosting callback. The released legacy overload either maps an explicitly supplied Client options instance or uses the normal resolver when its argument is null.

- [ ] **Step 6: Refactor LocalStackResource to store immutable package-owned state**

Keep `ILocalStackResource.Options` and the released constructor for metadata compatibility and implement them as adapters. Store `LocalStackHostingState` as the package-owned source of truth. Do not apply `[Obsolete]` yet because Task 4 first removes internal and ordinary repository usage.

- [ ] **Step 7: Run the AddLocalStack and resource-state tests**

```bash
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj
```

- [ ] **Step 8: Review checkpoint**

Proposed commit after explicit approval: `feat: add compatible LocalStack hosting overloads`

---

### Task 4: Internal Runtime Translation And Deprecations

**Files:**
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackCloudFormationResourceExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackProjectExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResource.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Configuration/LocalStackConfigurationExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Internal/LocalStackCdkCredentialsOverride.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestUtilities/LocalStackAssertions.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Core/LocalStackResourceTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Core/PublicApiCompatibilityTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackResourceConfiguratorTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackCdkCredentialsOverrideTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/LocalStackProjectExtensionsTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/LocalStackCloudFormationResourceExtensionsTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/AddAWSCDKBootstrapCfTemplateForLocalStackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/AddLocalStackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Integration.Tests/EagerLoadedServices/EagerLoadedServicesTests.cs`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`
- Modify: `playground/provisioning/LocalStack.Provisioning.CDK.AppHost/Program.cs`
- Modify: `playground/provisioning/LocalStack.Provisioning.CloudFormation.AppHost/Program.cs`

**Interfaces:**
- Consumes: `LocalStackHostingState` from the resource.
- Produces: unchanged LocalStack.Client runtime behavior, unchanged workload environment contracts, migrated repository call sites, and obsolete compatibility metadata.

- [ ] **Step 1: Rewrite runtime tests and add failing obsolete-metadata tests**

Keep exact assertions for:

```text
LocalStack__UseLocalStack
LocalStack__Session__AwsAccessKeyId
LocalStack__Session__AwsAccessKey
LocalStack__Session__AwsSessionToken
LocalStack__Session__RegionName
LocalStack__Config__LocalStackHost
LocalStack__Config__UseSsl
LocalStack__Config__UseLegacyPorts
LocalStack__Config__EdgePort
```

Assert host and edge port come from the allocated Aspire endpoint, not package-owned options.

Add reflection assertions that `[Obsolete(IsError = false)]` exists on the released Client-options overload, `AddLocalStackOptions`, `ILocalStackResource.Options`, the legacy `LocalStackResource` constructor, and all seven `LocalStackConfigurationExtensions` methods. Assert `UseLocalStack` and `AwsService` are not obsolete.

- [ ] **Step 2: Run the affected tests and verify they fail against Client-options signatures**

- [ ] **Step 3: Change internal signatures to LocalStackHostingState**

Construct `SessionOptions` and `ConfigOptions` only inside the runtime adapter immediately before `SessionStandalone`. Use actual endpoint host, scheme/compatibility SSL, and port. Create `SessionAWSCredentials` from state for the CDK override.

- [ ] **Step 4: Remove internal reads of obsolete `ILocalStackResource.Options`**

Text-search production source for `.Resource.Options` and `ILocalStackOptions`. Remaining occurrences must be limited to compatibility adapters and obsolete public signatures.

- [ ] **Step 5: Migrate ordinary repository call sites to the package-owned path**

Update unit tests, integration tests, and playground AppHosts that bind the released optional overload. Supply the explicit resource name required by the approved AWS/container overload or use the hosting-options callback when tests need custom enabled/disabled state. Only dedicated compatibility tests may invoke obsolete members; wrap those exact calls in the narrowest possible `#pragma warning disable CS0618` / restore pair. Do not add project-wide `NoWarn` or suppressions.

- [ ] **Step 6: Apply standard obsolete messages**

Use non-error CS0618 without custom diagnostic IDs. Every message must name its replacement and say removal occurs in the next major version. Do not obsolete `UseLocalStack` or `AwsService`.

- [ ] **Step 7: Run unit tests on net8.0, net9.0, and net10.0**

```bash
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj --framework net8.0
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj --framework net9.0
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj --framework net10.0
```

Confirm each run discovers more than zero tests and all discovered tests pass. Inspect the implemented signatures against `docs/plans/ws3-addlocalstack-overload-compatibility-spike.md`; do not claim these target-framework runs use different SDK compilers.

- [ ] **Step 8: Review checkpoint**

Proposed commit after explicit approval: `feat: isolate and deprecate LocalStack client hosting APIs`

---

### Task 5: Native Endpoint Conflict Warning

**Files:**
- Create: `src/Aspire.Hosting.LocalStack/Annotations/LocalStackEndpointConflictWarningAnnotation.cs`
- Create: `src/Aspire.Hosting.LocalStack/Internal/LocalStackEndpointConflictWarning.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`
- Create: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackEndpointConflictWarningTests.cs`

**Interfaces:**
- Consumes: `LocalStackEnabledAnnotation`, `BeforeStartEvent`, `ResourceLoggerService`, and environment annotations.
- Produces: a read-only, best-effort warning; no environment mutation.

- [ ] **Step 1: Add tests based on the verified throwaway probe**

Cover:

```text
LocalStack__UseLocalStack=true + AWS_ENDPOINT_URL -> one warning
LocalStack__UseLocalStack=true + AWS_ENDPOINT_URL_SQS -> one warning
case-insensitive key matching -> one warning
LocalStack__UseLocalStack=false -> no warning
native endpoint without LocalStackEnabledAnnotation -> no warning
warning callback -> environment dictionary unchanged
duplicate registration -> one annotation and one warning
endpoint callback appended after warning callback -> documented false-negative limitation
```

Use `ExecutionConfigurationBuilder.WithEnvironmentVariablesConfig()` and a capturing logger; do not start Docker.

- [ ] **Step 2: Run warning tests and verify they fail**

- [ ] **Step 3: Implement BeforeStart registration**

Select environment resources with `LocalStackEnabledAnnotation`, add one marker annotation, capture `ResourceLoggerService.GetLogger(resource)`, and append the warning callback. Do not use `EnvironmentCallbackContext.Logger` because dependency discovery can cache execution under `NullLogger`.

- [ ] **Step 4: Implement exact detection**

Parse `LocalStack__UseLocalStack` as a Boolean. Match `AWS_ENDPOINT_URL` and the `AWS_ENDPOINT_URL_` prefix with `StringComparison.OrdinalIgnoreCase`. Log the resource name and remediation; never log endpoint or credential values.

- [ ] **Step 5: Run warning tests and confirm they pass**

- [ ] **Step 6: Review checkpoint**

Proposed commit after explicit approval: `feat: warn on conflicting AWS endpoint configuration`

---

### Task 6: Consumer Documentation And Roadmap Alignment

**Files:**
- Modify: `README.md`
- Modify: `docs/CONFIGURATION.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/ROADMAP.md`
- Modify: `docs/agents/KNOWN_ISSUES.md`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/appsettings.json`
- Modify: `playground/provisioning/LocalStack.Provisioning.CDK.AppHost/appsettings.json`
- Modify: `playground/provisioning/LocalStack.Provisioning.CloudFormation.AppHost/appsettings.json`

**Interfaces:**
- Consumes: approved public and configuration interfaces.
- Produces: migration guidance and accurate WS3A/WS3B status.

- [ ] **Step 1: Replace recommended legacy configuration examples**

Use `Aspire:Hosting:LocalStack` and show environment names with double underscores. Keep a labeled legacy fallback example only in migration guidance.

- [ ] **Step 2: Document C# callback and fluent configuration**

Use the approved argument order:

```csharp
var localStack = builder.AddLocalStack(
    "localstack",
    awsConfig,
    options => options
        .WithEnabled(true)
        .WithRegion("us-east-1")
        .WithCredentials("accessKey", "secretKey", "token"),
    container => container.Lifetime = ContainerLifetime.Session);
```

- [ ] **Step 3: Record the native endpoint known issue in all required release documentation**

Keep these three locations aligned:

```text
README.md -> Known Limitations
docs/agents/KNOWN_ISSUES.md -> agent-facing triage note
CHANGELOG.md -> draft 13.4.1 Known Issues entry
```

State that LocalStack.Client proxy configuration and native `AWS_ENDPOINT_URL*` configuration must not be combined, affected AWSSDK.Core versions can sign a non-default-region request for `us-east-1`, and the package does not automatically emit or mutate native endpoint values. Cite the verified LocalStack.Client #27 runtime matrix. Do not add a native endpoint feature or a new `IResourceWithEndpoints` contract.

Create or update an unreleased `13.4.1` section and place the changelog note there. Remove any uncommitted draft copy under the released `13.4.0` section; do not rewrite released history as though the warning shipped in 13.4.0.

- [ ] **Step 4: Document the conflict warning limitation**

Call it best-effort. State that it does not mutate values and cannot observe callbacks appended after its BeforeStart registration.

- [ ] **Step 5: Verify ROADMAP WS3A/WS3B alignment**

Confirm WS3A contains the package-owned public interface, compatibility adapters, runtime conflict warning, and documentation duties. Confirm WS3B contains target-aware helper attachment and app-like resource coverage as research-only work with no selected solution. Keep WS3A `🔨 In progress` until Task 7 succeeds.

- [ ] **Step 6: Run Markdown checks**

Run `git diff --check` for changed Markdown files. Documentation-only commands do not require a .NET build.

- [ ] **Step 7: Review checkpoint**

Proposed commit after explicit approval: `docs: document LocalStack hosting migration`

---

### Task 7: End-To-End Verification

**Files:**
- Verify all files modified in Tasks 1-6.

**Interfaces:**
- Consumes: completed implementation.
- Produces: build, unit, integration, analyzer, and public-surface evidence.

- [ ] **Step 1: Restore and build**

```bash
dotnet restore
dotnet build --no-restore
```

Expected: zero warnings and zero errors for net8.0, net9.0, and net10.0.

- [ ] **Step 2: Run all unit tests**

```bash
dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj
```

Expected: more than zero tests discovered, all passed.

- [ ] **Step 3: Run integration tests when Docker is available**

```bash
dotnet test --project tests/Aspire.Hosting.LocalStack.Integration.Tests/Aspire.Hosting.LocalStack.Integration.Tests.csproj
```

Expected: more than zero tests discovered, all passed. If Docker or LocalStack runtime prerequisites are unavailable, record the exact blocker instead of claiming success.

- [ ] **Step 4: Run Slopwatch**

```bash
slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"
```

Expected: no warnings.

- [ ] **Step 5: Inspect final public and dependency surfaces**

Confirm no new package dependency was added, Client-owned types appear only in obsolete compatibility interfaces/internal adapters, `UseLocalStack` remains non-obsolete, the conflict warning does not mutate environment values, the three known-issue locations agree, and the implemented overload signatures exactly match the family previously compiled under SDK 8/C# 12 in `docs/plans/ws3-addlocalstack-overload-compatibility-spike.md`.

- [ ] **Step 6: Final review checkpoint and roadmap completion**

After a clean final review and all required verification, set WS3A to `✅ Done` and run `git diff --check` for the roadmap change. WS3B remains unchanged. Present the complete change summary, verification evidence, residual risks, and proposed Conventional Commit message. Do not commit, push, or create a pull request without explicit approval.
