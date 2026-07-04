# WS2 DynamoDB Streams Adapter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LocalStack support for AWS Aspire's DynamoDB Streams Lambda event-source helper and demonstrate it with async QR generation, a QR status route, and a control-room web frontend in the existing Lambda playground.

**Architecture:** Mirror the existing SQS event-source adapter with the smallest package change: type-name detection, LocalStack reference attachment, and environment-variable endpoint injection for the external Lambda Test Tool process (service-specific endpoints guarded so pre-existing values win). Keep broader helper-resource abstraction and LocalStack image/auth-token migration out of WS2, but record those follow-ups in WS6/WS7. Extend the existing URL shortener playground so DynamoDB table inserts drive QR generation through DynamoDB Streams, expose QR readiness through a `GET /{slug}/qr` API route, and add a single-page control-room frontend that visualizes the CDC path next to the SQS analytics path.

**Tech Stack:** .NET 10 SDK from `global.json`; `Aspire.Hosting 13.4.6`; `Aspire.Hosting.AWS 13.3.1`; `LocalStack.Client 2.0.0`; TUnit on Microsoft.Testing.Platform; AWS SDK v4; AWS Lambda Test Tool; LocalStack default image remains pinned by existing code.

## Global Constraints

- Work on branch `feature/ws2-dynamodb-streams`.
- Do not commit unless Deniz explicitly approves; if the plan's commit checkpoints conflict with the current session's batching preference, record the intended commit message and continue without committing.
- Do not edit package XML by hand when adding/removing package references. Use `dotnet add ... package`, `dotnet remove ... package`, `dotnet add ... reference`, and `dotnet sln ... add`.
- Do not change CI, release, package publishing, or LocalStack image/auth-token behavior in WS2.
- Do not introduce a general helper-resource abstraction in WS2; use minimal SQS-parity branches and leave the abstraction debt in WS6.
- Do not add DynamoDB Streams integration tests until WS7 resolves the LocalStack image/auth-token strategy.
- TUnit/MTP filters must be after `--`; never use VSTest-style `--filter` before `--`. Before relying on `--filter-class`/`--filter-method`, confirm the pinned TUnit version accepts them (fall back to `--treenode-filter` if not), and confirm every filtered run reports more than zero tests.
- After code/test changes, run Slopwatch if available: `slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"`.

---

## File Structure

- Modify `src/Aspire.Hosting.LocalStack/Internal/Constants.cs`: add the upstream internal DynamoDB Streams helper type-name constant.
- Modify `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs`: add DynamoDB Streams helper environment wiring.
- Modify `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`: detect the new helper executable during `UseLocalStack()` scans and attach `WithReference(localstack)`.
- Modify `src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs`: dispatch the referenced DynamoDB Streams helper to the new configurator.
- Modify `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/ConstantsTests.cs`: add reflection guard coverage for the new upstream internal helper type.
- Modify `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackResourceConfiguratorTests.cs`: verify the new environment callback is attached and emits global plus service-specific endpoint variables.
- Modify `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs`: verify `UseLocalStack()` recognizes the helper resource and adds LocalStack reference/wait annotations.
- Modify `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs`: verify callback dispatch configures the DynamoDB Streams helper.
- Modify `playground/lambda/LocalStack.Lambda.AppHost/UrlShortenerStack.cs`: enable DynamoDB Streams on `UrlsTable`.
- Modify `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`: add `QrCodeGeneratorLambda` and wire it with `WithDynamoDBStreamsEventSource(...)`.
- Modify `playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj`: add a project reference to the new QR generator Lambda using `dotnet add ... reference`.
- Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj`: Lambda project for stream processing.
- Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/Function.cs`: DynamoDB Streams handler that generates QR PNGs and updates URL item metadata.
- Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/QrCodeBitmapExtensions.cs`: local QR-to-PNG helper moved from the synchronous URL shortener path.
- Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/appsettings.json` and `appsettings.Development.json`: same LocalStack config defaults as existing Lambda projects.
- Modify `playground/lambda/LocalStack.Lambda.UrlShortener/Function.cs`: remove synchronous QR generation and write `QrStatus = Pending` on URL creation.
- Delete `playground/lambda/LocalStack.Lambda.UrlShortener/QrCodeBitmapExtensions.cs` and `S3UrlService.cs`: QR generation moves to the stream processor.
- Modify `playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj`: remove no-longer-used S3/QR package references using `dotnet remove ... package`.
- Modify `playground/LocalStack.Playground.ServiceDefaults/ActivitySources/QrCodeGeneratorActivitySource.cs`: add QR processor telemetry source.
- Modify `playground/LocalStack.Playground.ServiceDefaults/LocalStackPlaygroundExtensions.cs`: register the new QR activity source.
- Modify `playground/lambda/LocalStack.Lambda.Redirector/Function.cs`: add the `GET /{slug}/qr` route branch (404 unknown / 202 pending / 302 presigned S3 URL).
- Modify `playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj`: add `AWSSDK.S3` using `dotnet add ... package`.
- Create `playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj`: ASP.NET Core control-room frontend project.
- Create `playground/lambda/LocalStack.Lambda.Frontend/Program.cs`: minimal API (config, links, analytics, shorten-proxy endpoints) plus static file serving.
- Create `playground/lambda/LocalStack.Lambda.Frontend/Properties/launchSettings.json`: http launch profile for the frontend.
- Create `playground/lambda/LocalStack.Lambda.Frontend/wwwroot/index.html`, `wwwroot/app.js`, `wwwroot/styles.css`: single-page control room (shorten form, link list with QR cells, analytics feed) in framework-free HTML/JS.
- Create `playground/lambda/LocalStack.Lambda.Frontend/appsettings.json` and `appsettings.Development.json`: LocalStack config defaults matching other playground projects.
- Modify `Directory.Packages.props`: add `Amazon.Lambda.DynamoDBEvents` through `dotnet add ... package Amazon.Lambda.DynamoDBEvents --version 4.0.0`.
- Modify `LocalStack.sln`: add the new project through `dotnet sln LocalStack.sln add ...`.

---

### Task 1: Add DynamoDB Streams Helper Constants And Configurator

**Files:**
- Modify: `src/Aspire.Hosting.LocalStack/Internal/Constants.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/ConstantsTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackResourceConfiguratorTests.cs`

**Interfaces:**
- Consumes: existing `ConfigureSqsEventSourceResource(IResourceBuilder<ExecutableResource>, Uri, ILocalStackOptions)` pattern.
- Produces: `Constants.DynamoDbStreamsEventSourceResource` and `LocalStackResourceConfigurator.ConfigureDynamoDbStreamsEventSourceResource(IResourceBuilder<ExecutableResource>, Uri, ILocalStackOptions)`.

- [ ] **Step 1: Add failing constant guard tests**

In `ConstantsTests.cs`, add these tests next to the existing SQS guard tests:

```csharp
[Test]
public async Task DynamoDbStreamsEventSourceResource_Should_Have_Correct_Type_Name()
{
#pragma warning disable TUnitAssertions0005 // These tests intentionally verify constant values
    await Assert.That(Constants.DynamoDbStreamsEventSourceResource).IsEqualTo("Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource");
#pragma warning restore TUnitAssertions0005
}

[Test]
public async Task DynamoDbStreamsEventSourceResource_Type_Should_Exist_In_AWS_Assembly()
{
    var type = GetTypeByName(Constants.DynamoDbStreamsEventSourceResource);
    await Assert.That(type).IsNotNull();
    await Assert.That(type!.FullName).IsEqualTo(Constants.DynamoDbStreamsEventSourceResource);

    await Assert.That(typeof(ExecutableResource).IsAssignableFrom(type)).IsTrue()
        .Because($"Type {Constants.DynamoDbStreamsEventSourceResource} should inherit from ExecutableResource");
}
```

Also add the new type name to the existing `[Arguments]` list:

```csharp
[Arguments("Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource")]
```

- [ ] **Step 2: Run constant tests and verify they fail**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-class "*.ConstantsTests"
```

Expected: FAIL because `Constants.DynamoDbStreamsEventSourceResource` does not exist. Confirm the output reports more than zero tests discovered.

- [ ] **Step 3: Add the constant**

In `Constants.cs`, add this beside `SQSEventSourceResource`:

```csharp
internal const string DynamoDbStreamsEventSourceResource = "Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource";
```

- [ ] **Step 4: Run constant tests and verify they pass**

Run the same command from Step 2.

Expected: PASS, with more than zero tests executed.

- [ ] **Step 5: Add failing configurator environment test**

In `LocalStackResourceConfiguratorTests.cs`, add this test near the SQS configurator tests:

```csharp
[Test]
public async Task ConfigureDynamoDbStreamsEventSourceResource_Should_Emit_Global_And_Service_Specific_AWS_Endpoints()
{
    var executableResource = new ExecutableResource("test-ddb-streams-resource", "test-command", "test-workdir");
    var builder = Substitute.For<IResourceBuilder<ExecutableResource>>();
    var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(regionName: "eu-central-1");

    builder.Resource.Returns(executableResource);
    builder.WithAnnotation(Arg.Do<EnvironmentCallbackAnnotation>(executableResource.Annotations.Add), Arg.Any<ResourceAnnotationMutationBehavior>())
        .Returns(builder);

    var localStackUrl = new Uri("http://localhost:4566");

    LocalStackResourceConfigurator.ConfigureDynamoDbStreamsEventSourceResource(builder, localStackUrl, options);

    var envAnnotation = executableResource.Annotations.OfType<EnvironmentCallbackAnnotation>().Single();
    var env = new Dictionary<string, object>(StringComparer.Ordinal);
    var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), executableResource, env);

    await envAnnotation.Callback(context);

    await Assert.That(env["AWS_ENDPOINT_URL"]).IsEqualTo("http://localhost:4566/");
    await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB"]).IsEqualTo("http://localhost:4566/");
    await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"]).IsEqualTo("http://localhost:4566/");
    await Assert.That(env["AWS_ACCESS_KEY_ID"]).IsEqualTo("test-key");
    await Assert.That(env["AWS_SECRET_ACCESS_KEY"]).IsEqualTo("test-secret");
    await Assert.That(env["AWS_SESSION_TOKEN"]).IsEqualTo("test-token");
    await Assert.That(env["AWS_DEFAULT_REGION"]).IsEqualTo("eu-central-1");
}
```

*(Post-execution amendment, 2026-07-04: an endpoint-precedence guard test originally added here was removed together with the configurator's don't-clobber guard after DynamoDB Local coexistence was rejected fail-fast in `UseLocalStack()` — see the design doc's Package Design section.)*

- [ ] **Step 6: Run configurator test and verify it fails**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-method "*ConfigureDynamoDbStreamsEventSourceResource_Should*"
```

Expected: FAIL (both tests) because `ConfigureDynamoDbStreamsEventSourceResource` does not exist. Confirm more than zero tests are reported.

- [ ] **Step 7: Add the configurator method**

In `LocalStackResourceConfigurator.cs`, add this method after `ConfigureSqsEventSourceResource`:

```csharp
/// <summary>
/// Configures a DynamoDB Streams event source resource with LocalStack environment variables.
/// The helper is an external Lambda Test Tool process, so endpoint routing must use AWS SDK environment variables.
/// </summary>
/// <param name="resourceBuilder">The DynamoDB Streams event source resource to configure.</param>
/// <param name="localStackUrl">The LocalStack URL.</param>
/// <param name="options">The LocalStack configuration options.</param>
internal static void ConfigureDynamoDbStreamsEventSourceResource(IResourceBuilder<ExecutableResource> resourceBuilder, Uri localStackUrl, ILocalStackOptions options)
{
    resourceBuilder.WithEnvironment(context =>
    {
        var endpoint = localStackUrl.ToString();
        context.EnvironmentVariables["AWS_ENDPOINT_URL"] = endpoint;
        context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB"] = endpoint;
        context.EnvironmentVariables["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"] = endpoint;
        context.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = options.Session.AwsAccessKeyId;
        context.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = options.Session.AwsAccessKey;
        context.EnvironmentVariables["AWS_SESSION_TOKEN"] = options.Session.AwsSessionToken;
        context.EnvironmentVariables["AWS_DEFAULT_REGION"] = options.Session.RegionName;
    });
}
```

*(Post-execution amendment, 2026-07-04: the snippet originally guarded the two service-specific keys with `ContainsKey` checks; the guard was removed when DynamoDB Local coexistence was rejected fail-fast.)*

- [ ] **Step 8: Run task tests and verify they pass**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-class "*.ConstantsTests" --filter-method "*ConfigureDynamoDbStreamsEventSourceResource_Should*"
```

Expected: PASS, with more than zero tests executed.

- [ ] **Step 9: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add src/Aspire.Hosting.LocalStack/Internal/Constants.cs src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/ConstantsTests.cs tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackResourceConfiguratorTests.cs
git commit -m "feat: add DynamoDB Streams LocalStack configurator"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 2: Wire Helper Detection Through UseLocalStack And Connection Callback

**Files:**
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`
- Modify: `src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs`

**Interfaces:**
- Consumes: `Constants.DynamoDbStreamsEventSourceResource` and `ConfigureDynamoDbStreamsEventSourceResource(...)` from Task 1.
- Produces: automatic LocalStack reference and callback dispatch for AWS Aspire DynamoDB Streams helper resources.

- [ ] **Step 1: Add a test helper for reflected executable resources**

In `UseLocalStackTests.cs`, add this private method at the bottom of the class:

```csharp
private static ExecutableResource CreateExecutableResourceByTypeName(string typeName, string name)
{
    // Assembly.Load fallback mirrors ConstantsTests: type discovery must not depend on whether
    // another test already forced Aspire.Hosting.AWS into the AppDomain.
    var type = AppDomain.CurrentDomain.GetAssemblies()
                   .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                   .FirstOrDefault(type => type is not null)
               ?? System.Reflection.Assembly.Load("Aspire.Hosting.AWS").GetType(typeName, throwOnError: false)
               ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the current assembly context.");

    return (ExecutableResource)(Activator.CreateInstance(type, name)
                                ?? throw new InvalidOperationException($"Type '{typeName}' could not be created."));
}
```

If the same helper is also needed in `LocalStackConnectionStringAvailableCallbackTests.cs`, duplicate it there for now. Do not introduce a shared abstraction in WS2.

- [ ] **Step 2: Add failing UseLocalStack detection test**

In `UseLocalStackTests.cs`, add:

```csharp
[Test]
public async Task UseLocalStack_Should_Configure_DynamoDb_Streams_Event_Source_Resources_With_LocalStack_Reference()
{
    await using var app = TestApplicationBuilder.Create(builder =>
    {
        var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
        var localStack = builder.AddLocalStack(localStackOptions: options);
        builder.AddResource(CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper"));

        builder.UseLocalStack(localStack);
    });

    var localStackResource = app.GetResource<ILocalStackResource>("localstack");
    var helperResource = app.GetResource<ExecutableResource>("ddb-streams-helper");

    await helperResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);
    await helperResource.ShouldWaitFor(localStackResource);
    await localStackResource.ShouldHaveReferenceToResource(helperResource);
}
```

- [ ] **Step 3: Run the UseLocalStack test and verify it fails**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-method "*.UseLocalStack_Should_Configure_DynamoDb_Streams_Event_Source_Resources_With_LocalStack_Reference"
```

Expected: FAIL because `UseLocalStack()` does not yet recognize the new helper. Confirm more than zero tests are reported.

- [ ] **Step 4: Extend UseLocalStack helper detection**

In `LocalStackResourceBuilderExtensions.cs`, replace the SQS-only branch with this branch:

```csharp
else if (resource is ExecutableResource eventSourceResource &&
         (string.Equals(eventSourceResource.GetType().FullName, Constants.SQSEventSourceResource, StringComparison.Ordinal) ||
          string.Equals(eventSourceResource.GetType().FullName, Constants.DynamoDbStreamsEventSourceResource, StringComparison.Ordinal)))
{
    builder.CreateResourceBuilder(eventSourceResource).WithReference(localStack);
}
```

- [ ] **Step 5: Run the UseLocalStack test and verify it passes**

Run the command from Step 3.

Expected: PASS, with more than zero tests executed.

- [ ] **Step 6: Add failing callback dispatch test**

In `LocalStackConnectionStringAvailableCallbackTests.cs`, add the reflected helper method from Step 1 and this test:

```csharp
[Test]
public async Task Callback_Should_Configure_DynamoDb_Streams_Event_Source_Resource()
{
    var builder = DistributedApplication.CreateBuilder([]);
    var localStackAnnotations = new ResourceAnnotationCollection();
    var helperAnnotations = new ResourceAnnotationCollection();
    var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: true, regionName: "eu-central-1");
    var helperResource = CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper");

    helperResource.Annotations.Add(new LocalStackEnabledAnnotation(Substitute.For<ILocalStackResource>()));
    foreach (var annotation in helperResource.Annotations.ToArray())
    {
        helperAnnotations.Add(annotation);
    }

    var connectionString = "http://localhost:4566";
    var localStackResource = Substitute.For<ILocalStackResource>();
    localStackResource.Name.Returns("localstack");
    localStackResource.Options.Returns(options);
    localStackResource.Annotations.Returns(localStackAnnotations);
    // ReferenceExpression.Create takes an interpolated-string handler; a bare string literal does not compile.
    localStackResource.ConnectionStringExpression.Returns(ReferenceExpression.Create($"{connectionString}"));

    localStackAnnotations.Add(new LocalStackReferenceAnnotation(helperResource));

    var callback = LocalStackConnectionStringAvailableCallback.CreateCallback(builder);

    await callback(localStackResource, null!, CancellationToken.None);

    var envAnnotation = helperResource.Annotations.OfType<EnvironmentCallbackAnnotation>().Single();
    var env = new Dictionary<string, object>(StringComparer.Ordinal);
    var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), helperResource, env);
    await envAnnotation.Callback(context);

    await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB"]).IsEqualTo("http://localhost:4566/");
    await Assert.That(env["AWS_ENDPOINT_URL_DYNAMODB_STREAMS"]).IsEqualTo("http://localhost:4566/");
    await Assert.That(env["AWS_DEFAULT_REGION"]).IsEqualTo("eu-central-1");
}
```

If the copied `helperAnnotations` setup proves unnecessary because `ExecutableResource.Annotations` is directly mutable, simplify the test by removing the substitute-only collection and keep the assertions unchanged.

- [ ] **Step 7: Run the callback test and verify it fails**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-method "*.Callback_Should_Configure_DynamoDb_Streams_Event_Source_Resource"
```

Expected: FAIL because callback dispatch does not yet call the DynamoDB Streams configurator. Confirm more than zero tests are reported.

- [ ] **Step 8: Extend callback dispatch**

In `LocalStackConnectionStringAvailableCallback.cs`, add this branch immediately after the existing SQS branch and before the CloudFormation relationship fallback:

```csharp
else if (resource is ExecutableResource dynamoDbStreamsResource &&
         string.Equals(dynamoDbStreamsResource.GetType().FullName, Constants.DynamoDbStreamsEventSourceResource, StringComparison.Ordinal))
{
    var executableResourceBuilder = builder.CreateResourceBuilder(dynamoDbStreamsResource);
    LocalStackResourceConfigurator.ConfigureDynamoDbStreamsEventSourceResource(executableResourceBuilder, localStackUrl, localStackOptions);
}
```

- [ ] **Step 9: Run task tests and verify they pass**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-class "*.UseLocalStackTests" --filter-class "*.LocalStackConnectionStringAvailableCallbackTests"
```

Expected: PASS, with more than zero tests executed.

- [ ] **Step 10: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs
git commit -m "feat: wire DynamoDB Streams helpers to LocalStack"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 3: Add QR Generator Lambda Playground Project

**Files:**
- Create: `playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj`
- Create: `playground/lambda/LocalStack.Lambda.QrCodeGenerator/Function.cs`
- Create: `playground/lambda/LocalStack.Lambda.QrCodeGenerator/QrCodeBitmapExtensions.cs`
- Create: `playground/lambda/LocalStack.Lambda.QrCodeGenerator/appsettings.json`
- Create: `playground/lambda/LocalStack.Lambda.QrCodeGenerator/appsettings.Development.json`
- Create: `playground/LocalStack.Playground.ServiceDefaults/ActivitySources/QrCodeGeneratorActivitySource.cs`
- Modify: `playground/LocalStack.Playground.ServiceDefaults/LocalStackPlaygroundExtensions.cs`
- Modify: `Directory.Packages.props` through CLI
- Modify: `LocalStack.sln` through CLI

**Interfaces:**
- Consumes: `UrlsTableName` and `QrBucketName` environment values from the AppHost.
- Produces: Lambda handler `LocalStack.Lambda.QrCodeGenerator.Function::FunctionHandler` accepting `Amazon.Lambda.DynamoDBEvents.DynamoDBEvent`.

- [ ] **Step 1: Create project directory and base project file**

Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj` with this initial content:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$(DefaultTargetFramework)</TargetFramework>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <PublishReadyToRun>true</PublishReadyToRun>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\LocalStack.Playground.ServiceDefaults\LocalStack.Playground.ServiceDefaults.csproj"/>
  </ItemGroup>

  <ItemGroup>
    <Content Update="appsettings.json">
      <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
    <Content Update="appsettings.Development.json">
      <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      <DependentUpon>appsettings.json</DependentUpon>
    </Content>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add package references using CLI**

Run these commands from the repository root:

```powershell
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package AWSSDK.Core
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package AWSSDK.DynamoDBv2
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package AWSSDK.S3
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package Amazon.Lambda.Core
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package Amazon.Lambda.DynamoDBEvents --version 4.0.0
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package Amazon.Lambda.Serialization.SystemTextJson
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package LocalStack.Client
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package LocalStack.Client.Extensions
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package Net.Codecrete.QrCodeGenerator
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package SkiaSharp
dotnet add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj" package SkiaSharp.NativeAssets.Linux
```

Expected: project file receives package references without inline versions; `Directory.Packages.props` receives `Amazon.Lambda.DynamoDBEvents` version `4.0.0`.

- [ ] **Step 3: Add appsettings files**

Create `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "LocalStack": {
    "UseLocalStack": false
  }
}
```

Create `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "LocalStack": {
    "UseLocalStack": true
  }
}
```

- [ ] **Step 4: Move QR PNG helper to the new project**

Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/QrCodeBitmapExtensions.cs` by copying the current implementation from `LocalStack.Lambda.UrlShortener/QrCodeBitmapExtensions.cs` and changing the namespace to:

```csharp
namespace LocalStack.Lambda.QrCodeGenerator;
```

- [ ] **Step 5: Add QR activity source**

Create `playground/LocalStack.Playground.ServiceDefaults/ActivitySources/QrCodeGeneratorActivitySource.cs`:

```csharp
using System.Diagnostics;

namespace LocalStack.Playground.ServiceDefaults.ActivitySources;

public static class QrCodeGeneratorActivitySource
{
    public const string ActivitySourceName = "LocalStack.Lambda.QrCodeGenerator";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
```

In `LocalStackPlaygroundExtensions.cs`, add this source to tracing after `UrlShortenerActivitySource`:

```csharp
.AddSource(UrlShortenerActivitySource.ActivitySourceName)
.AddSource(QrCodeGeneratorActivitySource.ActivitySourceName)
.AddSource(RedirectorActivitySource.ActivitySourceName);
```

- [ ] **Step 6: Add the DynamoDB Streams Lambda handler**

Create `playground/lambda/LocalStack.Lambda.QrCodeGenerator/Function.cs`:

```csharp
using System.Diagnostics;
using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.S3;
using Amazon.S3.Model;
using LocalStack.Client.Extensions;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Codecrete.QrCodeGenerator;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.QrCodeGenerator;

public class Function
{
    private const string InsertEventName = "INSERT";
    private readonly TracerProvider _traceProvider;
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonS3 _amazonS3;
    private readonly string _urlsTable;
    private readonly string _qrBucketName;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();
        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();
        builder.Services.AddAwsService<IAmazonS3>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();
        _amazonS3 = host.Services.GetRequiredService<IAmazonS3>();
        _urlsTable = builder.Configuration["AWS:Resources:UrlsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
        _qrBucketName = builder.Configuration["AWS:Resources:QrBucketName"] ?? throw new InvalidOperationException("Missing AWS:Resources:QrBucketName");
    }

    public Task FunctionHandler(DynamoDBEvent dynamoDbEvent, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (streamEvent, lambdaContext) =>
        {
            using var activity = QrCodeGeneratorActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));
            activity?.AddTag("record.count", streamEvent.Records.Count);
            lambdaContext.Logger.LogInformation($"Processing {streamEvent.Records.Count} DynamoDB stream records");

            foreach (var record in streamEvent.Records)
            {
                if (!string.Equals(record.EventName, InsertEventName, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    await ProcessInsertAsync(record, lambdaContext).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    lambdaContext.Logger.LogError($"Failed to generate QR code from DynamoDB stream record: {ex.Message}");
                }
            }
        }, dynamoDbEvent, context);
    }

    private async Task ProcessInsertAsync(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
    {
        using var activity = QrCodeGeneratorActivitySource.ActivitySource.StartActivity(nameof(ProcessInsertAsync));
        var newImage = record.Dynamodb.NewImage;
        var slug = newImage["Slug"].S;
        var url = newImage["Url"].S;
        var key = $"qr/{slug}.png";

        activity?.AddTag("slug", slug);
        activity?.AddTag("qr.bucket", _qrBucketName);
        activity?.AddTag("qr.key", key);

        var qrCode = QrCode.EncodeText(url, QrCode.Ecc.Quartile);
        var pngData = qrCode.ToPng(scale: 10, border: 4);

        await _amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _qrBucketName,
            Key = key,
            InputStream = new MemoryStream(pngData),
            ContentType = "image/png",
        }).ConfigureAwait(false);

        await _amazonDynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _urlsTable,
            Key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["Slug"] = new() { S = slug },
            },
            UpdateExpression = "SET QrStatus = :ready, QrObjectKey = :key, QrGeneratedAt = :generatedAt",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                [":ready"] = new() { S = "Ready" },
                [":key"] = new() { S = key },
                [":generatedAt"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            },
        }).ConfigureAwait(false);

        var sanitizedSlug = slug.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        context.Logger.LogInformation($"Generated QR code for slug: {sanitizedSlug}");
    }
}
```

- [ ] **Step 7: Add project to solution**

Run:

```powershell
dotnet sln "LocalStack.sln" add "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj"
```

Expected: `dotnet sln "LocalStack.sln" list` includes the new QR generator project.

- [ ] **Step 8: Build the new project**

Run:

```powershell
dotnet build "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj"
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 9: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add Directory.Packages.props LocalStack.sln playground/lambda/LocalStack.Lambda.QrCodeGenerator playground/LocalStack.Playground.ServiceDefaults/ActivitySources/QrCodeGeneratorActivitySource.cs playground/LocalStack.Playground.ServiceDefaults/LocalStackPlaygroundExtensions.cs
git commit -m "feat(playground): add QR generator stream Lambda"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 4: Wire Async QR Generation Into The Existing Lambda Playground

**Files:**
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/UrlShortenerStack.cs`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj` through CLI
- Modify: `playground/lambda/LocalStack.Lambda.UrlShortener/Function.cs`
- Modify: `playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj` through CLI
- Delete: `playground/lambda/LocalStack.Lambda.UrlShortener/QrCodeBitmapExtensions.cs`
- Delete: `playground/lambda/LocalStack.Lambda.UrlShortener/S3UrlService.cs`

**Interfaces:**
- Consumes: `LocalStack.Lambda.QrCodeGenerator.Function::FunctionHandler` from Task 3.
- Produces: playground AppHost graph where `UrlsTable` insert events trigger async QR generation through `WithDynamoDBStreamsEventSource(...)`.

- [ ] **Step 1: Enable DynamoDB Streams on `UrlsTable`**

In `UrlShortenerStack.cs`, update the `UrlsTable` `TableProps`:

```csharp
UrlsTable = new Table(this, "UrlsTable", new TableProps
{
    TableName = "Urls",
    PartitionKey = new Attribute { Name = "Slug", Type = AttributeType.STRING },
    BillingMode = BillingMode.PAY_PER_REQUEST,
    Stream = StreamViewType.NEW_IMAGE,
});
```

- [ ] **Step 2: Add AppHost project reference using CLI**

Run:

```powershell
dotnet add "playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj" reference "playground/lambda/LocalStack.Lambda.QrCodeGenerator/LocalStack.Lambda.QrCodeGenerator.csproj"
```

Expected: AppHost project references the new QR generator project.

- [ ] **Step 3: Wire the QR generator Lambda in Program.cs**

In `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`, add this after the analyzer Lambda block and before API Gateway setup:

```csharp
builder.AddAWSLambdaFunction<Projects.LocalStack_Lambda_QrCodeGenerator>(
        name: "QrCodeGeneratorLambda",
        lambdaHandler: "LocalStack.Lambda.QrCodeGenerator::LocalStack.Lambda.QrCodeGenerator.Function::FunctionHandler")
    .WithDynamoDBStreamsEventSource(urlShortenerStack.GetOutput("UrlsTableName"))
    .WithReference(urlShortenerStack);
```

- [ ] **Step 4: Remove synchronous QR dependencies from UrlShortener using CLI**

Run:

```powershell
dotnet remove "playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj" package AWSSDK.S3
dotnet remove "playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj" package Net.Codecrete.QrCodeGenerator
dotnet remove "playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj" package SkiaSharp
dotnet remove "playground/lambda/LocalStack.Lambda.UrlShortener/LocalStack.Lambda.UrlShortener.csproj" package SkiaSharp.NativeAssets.Linux
```

Expected: UrlShortener project no longer references S3 or QR packages.

- [ ] **Step 5: Delete old synchronous QR helper files**

Delete:

```text
playground/lambda/LocalStack.Lambda.UrlShortener/QrCodeBitmapExtensions.cs
playground/lambda/LocalStack.Lambda.UrlShortener/S3UrlService.cs
```

- [ ] **Step 6: Remove synchronous QR code from UrlShortener**

In `Function.cs`, remove these using directives:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Net.Codecrete.QrCodeGenerator;
```

Remove these fields:

```csharp
private readonly IAmazonS3 _amazonS3;
private readonly IS3UrlService _s3UrlService;
private readonly string _qrBucketName;
```

Remove these service registrations and assignments:

```csharp
builder.Services.AddAwsService<IAmazonS3>();
builder.Services.AddTransient<IS3UrlService, S3UrlService>();
_amazonS3 = host.Services.GetRequiredService<IAmazonS3>();
_s3UrlService = host.Services.GetRequiredService<IS3UrlService>();
_qrBucketName = builder.Configuration["AWS:Resources:QrBucketName"] ?? throw new InvalidOperationException("Missing AWS:Resources:QrBucketName");
```

Replace the handler section after `SendAnalyticsEventAsync(...)` with:

```csharp
var responseBody = JsonSerializer.Serialize(new ShortenResponse(slug, "Pending", $"/{slug}/qr"));

return Created(responseBody);
```

Replace `InsertRecordAsync` item attributes with:

```csharp
Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
{
    ["Slug"] = new() { S = slug },
    ["Url"] = new() { S = url },
    ["CreatedAt"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
    ["QrStatus"] = new() { S = "Pending" },
},
```

Delete the methods `UpdateRecordWithQrUrlAsync` and `GenerateAndUploadQrAsync`.

Replace the request/response records with:

```csharp
private sealed record ShortenRequest(string Url);

private sealed record ShortenResponse(string Id, string QrStatus, string QrPath);
```

- [ ] **Step 7: Build the playground AppHost**

Run:

```powershell
dotnet build "playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj"
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 8: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add playground/lambda/LocalStack.Lambda.AppHost playground/lambda/LocalStack.Lambda.UrlShortener
git commit -m "feat(playground): demonstrate async QR generation with DynamoDB Streams"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 5: Add QR Status Route To Redirector And API Gateway

**Files:**
- Modify: `playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj` through CLI
- Modify: `playground/lambda/LocalStack.Lambda.Redirector/Function.cs`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`

**Interfaces:**
- Consumes: `QrStatus`/`QrObjectKey` item attributes written by the stream processor (Tasks 3-4) and the `QrBucketName` stack output.
- Produces: public `GET /{slug}/qr` route returning 404 (unknown slug), 202 (pending), or 302 to a presigned S3 URL (ready).

- [ ] **Step 1: Add the S3 SDK to the Redirector using CLI**

```powershell
dotnet add "playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj" package AWSSDK.S3
```

- [ ] **Step 2: Register the route in the AppHost**

In `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`, capture the API Gateway emulator builder in a variable (Task 6 also needs it) and add the QR route:

```csharp
var apiGateway = builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    .WithReference(urlShortenerLambda, Method.Post, "/shorten")
    .WithReference(redirectorLambda, Method.Get, "/{slug}")
    .WithReference(redirectorLambda, Method.Get, "/{slug}/qr");
```

- [ ] **Step 3: Add the QR route branch to the Redirector**

In `Function.cs`: register `IAmazonS3` (`builder.Services.AddAwsService<IAmazonS3>()`), resolve it plus `AWS:Resources:QrBucketName` in the constructor using the same pattern as the existing lookups, and dispatch inside the existing traced handler before the redirect logic:

```csharp
if (string.Equals(proxyRequest.RequestContext?.RouteKey, "GET /{slug}/qr", StringComparison.Ordinal))
{
    return await HandleQrStatusAsync(slug, lambdaContext).ConfigureAwait(false);
}
```

If the emulator does not populate `RouteKey`, dispatch on the raw path suffix (`proxyRequest.RawPath` ending in `/qr`) instead — verify against the running emulator, do not assume.

Implement the handler (adapt naming/lookup to the existing style; the existing item lookup must return the attributes needed here):

```csharp
private async Task<APIGatewayHttpApiV2ProxyResponse> HandleQrStatusAsync(string slug, ILambdaContext context)
{
    var item = await GetUrlItemAsync(slug).ConfigureAwait(false);
    if (item is null)
    {
        return NotFound();
    }

    var isReady = item.TryGetValue("QrStatus", out var status)
                  && string.Equals(status.S, "Ready", StringComparison.Ordinal)
                  && item.TryGetValue("QrObjectKey", out var key);

    if (!isReady)
    {
        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Accepted,
            Body = JsonSerializer.Serialize(new { slug, qrStatus = "Pending" }),
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = "application/json" },
        };
    }

    var presignedUrl = await _amazonS3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
    {
        BucketName = _qrBucketName,
        Key = item["QrObjectKey"].S,
        Expires = DateTime.UtcNow.AddMinutes(5),
    }).ConfigureAwait(false);

    return new APIGatewayHttpApiV2ProxyResponse
    {
        StatusCode = (int)HttpStatusCode.Found,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Location"] = presignedUrl },
    };
}
```

- [ ] **Step 4: Build the Redirector and the AppHost**

```powershell
dotnet build "playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj"
dotnet build "playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj"
```

Expected: both builds succeed with zero warnings and zero errors.

- [ ] **Step 5: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add playground/lambda/LocalStack.Lambda.Redirector playground/lambda/LocalStack.Lambda.AppHost
git commit -m "feat(playground): expose QR readiness through GET /{slug}/qr"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 6: Add Control-Room Frontend Project

**Files:**
- Create: `playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj`
- Create: `playground/lambda/LocalStack.Lambda.Frontend/Program.cs`
- Create: `playground/lambda/LocalStack.Lambda.Frontend/Properties/launchSettings.json`
- Create: `playground/lambda/LocalStack.Lambda.Frontend/wwwroot/index.html`, `wwwroot/app.js`, `wwwroot/styles.css`
- Create: `playground/lambda/LocalStack.Lambda.Frontend/appsettings.json`, `appsettings.Development.json`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj` through CLI
- Modify: `LocalStack.sln` through CLI

**Interfaces:**
- Consumes: API Gateway emulator endpoint (`ApiGateway__BaseUrl` env), `AWS:Resources:UrlsTableName` / `AWS:Resources:AnalyticsTableName` outputs, and the `LocalStack__*` env injected by `UseLocalStack()`.
- Produces: a single-page control room at the frontend's http endpoint showing the CDC path (QR cells flipping pending → PNG) next to the SQS analytics feed.

- [ ] **Step 1: Create the project**

`LocalStack.Lambda.Frontend.csproj` uses `Microsoft.NET.Sdk.Web`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$(DefaultTargetFramework)</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\LocalStack.Playground.ServiceDefaults\LocalStack.Playground.ServiceDefaults.csproj"/>
  </ItemGroup>
</Project>
```

Add `Properties/launchSettings.json` with a plain http profile consistent with playground conventions.

- [ ] **Step 2: Add packages using CLI**

```powershell
dotnet add "playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj" package AWSSDK.DynamoDBv2
dotnet add "playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj" package LocalStack.Client
dotnet add "playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj" package LocalStack.Client.Extensions
```

- [ ] **Step 3: Add appsettings files**

Same LocalStack defaults as the other playground projects: `UseLocalStack: false` in `appsettings.json`, `true` in `appsettings.Development.json`.

- [ ] **Step 4: Implement the minimal API**

`Program.cs` responsibilities:

- `builder.AddServiceDefaults()`, `builder.Services.AddLocalStack(builder.Configuration)`, `builder.Services.AddAwsService<IAmazonDynamoDB>()`.
- A named `HttpClient` whose `BaseAddress` comes from the `ApiGateway:BaseUrl` configuration value.
- Endpoints:
  - `GET /api/config` → `{ apiGatewayBaseUrl }` so the browser can build short-link and QR `img` URLs that point directly at the API Gateway emulator.
  - `GET /api/links` → scan the Urls table, newest first, limit 25, projecting `Slug`, `Url`, `CreatedAt`, `QrStatus`, `QrObjectKey`. A scan is acceptable for a demo-sized table.
  - `GET /api/analytics` → scan the analytics table, newest first, limit 25.
  - `POST /api/shorten` → server-side proxy of the request body to API Gateway `POST /shorten` (avoids browser CORS against the emulator).
- `app.UseDefaultFiles(); app.UseStaticFiles();` to serve the page.
- Table names come from `AWS:Resources:UrlsTableName` and `AWS:Resources:AnalyticsTableName` (both outputs already exist in the AppHost).

- [ ] **Step 5: Implement the static page**

`wwwroot/index.html` + `app.js` + `styles.css`, framework-free:

- Shorten form posting to `/api/shorten`; the new row appears immediately with its `QrStatus`.
- Poll `/api/links` and `/api/analytics` every ~1500 ms; render the link table (short URL `{apiBase}/{slug}`, QR cell) and the analytics feed (`url_created` / `url_accessed` events).
- QR cell: while `QrStatus` is not `Ready`, show a pending badge/spinner; when `Ready`, set `<img src="{apiBase}/{slug}/qr">` — the browser follows the 302 to the presigned URL and renders the PNG. This pending→image flip is the eventual-consistency moment the demo exists for.

- [ ] **Step 6: Wire the frontend into the AppHost**

```powershell
dotnet add "playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj" reference "playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj"
```

In `Program.cs`, after the API Gateway emulator block and before `UseLocalStack(localstack)`:

```csharp
builder.AddProject<Projects.LocalStack_Lambda_Frontend>("Frontend")
    .WithReference(urlShortenerStack)
    .WithEnvironment("ApiGateway__BaseUrl", apiGateway.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);
```

`WithReference(urlShortenerStack)` is what routes the frontend through `UseLocalStack()`'s project-configuration branch (`LocalStack__*` env injection) — it must be registered before the `UseLocalStack(localstack)` call.

- [ ] **Step 7: Add the project to the solution**

```powershell
dotnet sln "LocalStack.sln" add "playground/lambda/LocalStack.Lambda.Frontend/LocalStack.Lambda.Frontend.csproj"
```

- [ ] **Step 8: Build the solution**

```powershell
dotnet build "LocalStack.sln"
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 9: Commit checkpoint if approved**

If Deniz has approved per-task commits, run:

```powershell
git add LocalStack.sln playground/lambda/LocalStack.Lambda.Frontend playground/lambda/LocalStack.Lambda.AppHost
git commit -m "feat(playground): add control-room frontend for stream and queue paths"
```

If commits are being batched, record this intended message and do not commit.

---

### Task 7: Final Verification And Documentation Alignment

**Files:**
- Review: `docs/plans/ws2-dynamodb-streams-adapter-design.md`
- Review: `docs/ROADMAP.md`
- Review: source/test/playground files changed by Tasks 1-6

**Interfaces:**
- Consumes: completed adapter and playground tasks.
- Produces: verified WS2 implementation ready for final review and commit approval.

- [ ] **Step 1: Run targeted unit tests**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --framework net10.0 --no-launch-profile -- --filter-class "*.ConstantsTests" --filter-class "*.LocalStackResourceConfiguratorTests" --filter-class "*.UseLocalStackTests" --filter-class "*.LocalStackConnectionStringAvailableCallbackTests"
```

Expected: PASS, with more than zero tests executed.

- [ ] **Step 2: Run full unit test project**

Run:

```powershell
dotnet test --project "tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj" --no-launch-profile
```

Expected: PASS across all target frameworks, with more than zero tests executed.

- [ ] **Step 3: Build the solution**

Run:

```powershell
dotnet build "LocalStack.sln"
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 4: Run Slopwatch if available**

Run:

```powershell
slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"
```

Expected: no warnings. If `slopwatch` is not installed, record the command failure and do not claim Slopwatch passed.

- [ ] **Step 5: Review docs for consistency**

Confirm these statements remain true:

```text
docs/plans/ws2-dynamodb-streams-adapter-design.md says integration tests are deferred to WS7.
docs/ROADMAP.md links WS2 research, design, and implementation plan.
docs/ROADMAP.md keeps WS6 helper-resource cleanup and WS7 image/auth-token debt visible.
docs/ROADMAP.md WS3 records the endpoint-precedence contract and target-aware attachment insights from WS2.
docs/ROADMAP.md WS2 status reflects implementation progress (🔨 while in progress, ✅ when done).
```

- [ ] **Step 6: Inspect git diff before commit approval**

Run:

```powershell
git status --short
git diff --stat
git diff --check
```

Expected: only intentional WS2 files are changed, `git diff --check` has no output.

- [ ] **Step 7: Ask for final commit approval**

Present a concise summary and proposed commit message:

```text
Summary:
- Added LocalStack wiring for AWS Aspire DynamoDB Streams helper resources with guarded endpoint precedence.
- Added unit coverage for reflected helper type, endpoint env injection, the DynamoDB Local fail-fast, UseLocalStack detection, and callback dispatch.
- Extended the Lambda playground with async QR generation driven by DynamoDB Streams, a GET /{slug}/qr status route, and a control-room frontend showing the CDC and SQS paths side by side.
- Preserved WS3/WS6/WS7 follow-up debt for endpoint-precedence contract, helper-resource cleanup, and LocalStack image/auth-token strategy.

Proposed commit:
feat(ws2): support DynamoDB Streams event-source helpers
```

Do not commit until Deniz approves.
