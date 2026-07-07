# Post-review Followups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply nine post-review follow-up changes from the `02d386278e` self-review on the `feature/ws2-dynamodb-streams-squashed` branch as a single squash-merged PR.

**Architecture:** Nine independent concerns grouped into docs, refactor, behavior change, and build. Each task is self-contained — independently testable, commitable, and revertable. Risk is ordered low → medium: docs/comments first, then internal refactor, then test refactor, then production behavior change, then build infrastructure last (so a build breakage does not mask earlier regressions).

**Tech Stack:** .NET SDK 10.0.301, TUnit 1.56.18 on Microsoft.Testing.Platform, Aspire 13.4.6, Aspire.Hosting.AWS 13.3.1, AWSSDK.S3 4.0.25.2, LocalStack.Client 2.0.0.

## Global Constraints

- Branch: `feature/ws2-dynamodb-streams-squashed` (no new branch).
- Squash merge: sub-commits during development are free-form; PR title is the canonical message.
- Conventional Commit types only (`docs`, `chore`, `refactor`, `fix`, `test`, `build`). No AI attribution trailers.
- Central Package Management: never hand-edit package versions into individual `.csproj` files.
- Warnings-as-errors is enabled; analyzers include Meziantou, Roslynator, Sonar, BannedApi.
- TUnit on MTP: use `dotnet test --project <csproj>`; avoid plain `--filter` (silently runs zero tests); confirm total > 0.
- All AWS SDK configuration uses environment-variable-driven endpoints (`AWS_ENDPOINT_URL*`, `AWS_DEFAULT_REGION`, credentials) — no hard-coded regional hosts.
- `external/` tree is local-only and gitignored; do not commit anything under it.

**Reference spec:** `docs/superpowers/specs/2026-07-07-review-followups-design.md`

---

## File Structure

### Files Created
- `src/Aspire.Hosting.LocalStack/Annotations/DynamoDbLocalGuardAnnotation.cs` — relocated annotation, internal sealed class.
- `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestInfrastructure/TestResourceFactory.cs` — shared static helper for creating `ExecutableResource` by type name.
- `tests/Aspire.Hosting.LocalStack.Integration.Tests/LocalStack/DynamoDbLocalFailFastTests.cs` — test moved out of `Playground/Lambda/` and rewritten with ad-hoc `DistributedApplicationTestingBuilder.Create()`.
- `playground/lambda/LocalStack.Lambda.Redirector/S3UrlServiceTests.cs` — new unit tests for presigned-URL behavior (real AWS path) and LocalStack path.
- `LocalStack.slnx` — new solution file (XML-based, replaces `.sln`).

### Files Modified
- `docs/CONFIGURATION.md` — ten locations replace hardcoded `4.12.0` with placeholders.
- `README.md` — one location replace hardcoded `4.12.0` with placeholder.
- `playground/lambda/LocalStack.Lambda.AppHost/Program.cs` — delete misleading API Gateway comment (lines 47-49).
- `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs` — remove the inline `DynamoDbLocalGuardAnnotation` private nested class (line 337); update its usages to the relocated type.
- `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs` — replace local `CreateExecutableResourceByTypeName` with shared helper import.
- `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs` — replace local `CreateExecutableResourceByTypeName` with shared helper import.
- `playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs` — real-AWS branch uses `GetPreSignedURL()`.
- `playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj` — add reference to test project or restructure for test access (see Task 6).
- `global.json` — SDK pin `10.0.100` → `10.0.301`.
- `.gitignore` — remove line 432 (ignore for `playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json`).
- `LocalStack.sln` — deleted after `.slnx` migration.
- `LocalStack.slnx` — added; reflects the same project set without `Debug|x64`/`Debug|x86` platform configurations.

---

## Task 1: Replace hardcoded image-tag versions in CONFIGURATION.md and README

**Files:**
- Modify: `docs/CONFIGURATION.md` (10 locations)
- Modify: `README.md:123`

**Interfaces:**
- Consumes: n/a (documentation-only)
- Produces: version-agnostic docs that don't need maintenance on each LocalStack image bump.

- [ ] **Step 1: Update the Quick Reference table in CONFIGURATION.md**

Replace line 14:
```markdown
| `ContainerImageTag` | `string?` | `null` (`4.12.0`) | Custom container image tag/version |
```
with:
```markdown
| `ContainerImageTag` | `string?` | `null` (current LocalStack tag) | Custom container image tag/version |
```

- [ ] **Step 2: Update example values throughout CONFIGURATION.md**

For each occurrence of `container.ContainerImageTag = "4.12.0";` in code examples (lines 188, 205, 214, 224, 240, 274, 402), replace with:
```csharp
container.ContainerImageTag = "4.x.x"; // Pin to a specific LocalStack version
```

For the Artifactory/ACR/ECR "Pulls" comment lines (206, 215, 224), update the example tag in the comment to match.

- [ ] **Step 3: Update defaults block in CONFIGURATION.md**

Lines 194-196 currently say:
```markdown
- `ContainerRegistry`: `docker.io` (Docker Hub)
- `ContainerImage`: `localstack/localstack`
- `ContainerImageTag`: `4.12.0`
```
Change the third line to:
```markdown
- `ContainerImageTag`: the LocalStack tag this package currently pins (check `Directory.Packages.props` or the runtime default)
```

- [ ] **Step 4: Update README.md**

Replace line 123:
```markdown
- **`ContainerImageTag`** - Custom image tag/version (default: `4.12.0`). Use to pin to a specific LocalStack version
```
with:
```markdown
- **`ContainerImageTag`** - Custom image tag/version (default: the current LocalStack tag). Use to pin to a specific LocalStack version
```

- [ ] **Step 5: Verify no remaining hardcoded version strings**

Run: `grep -rn "4\.12\.0" docs/CONFIGURATION.md README.md`
Expected: zero matches.

- [ ] **Step 6: Commit**

```bash
git add docs/CONFIGURATION.md README.md
git commit -m "docs: replace hardcoded image tag with placeholders"
```

---

## Task 2: Delete misleading API Gateway emulator comment

**Files:**
- Modify: `playground/lambda/LocalStack.Lambda.AppHost/Program.cs:47-49`

**Interfaces:**
- Consumes: n/a
- Produces: cleaner playground source.

- [ ] **Step 1: Delete the comment**

Remove lines 47-49 from `playground/lambda/LocalStack.Lambda.AppHost/Program.cs`:
```csharp
// The API Gateway emulator stores one route per Lambda resource (its route-config environment variable is
// keyed by resource name, so a second WithReference overwrites the first). The QR status route therefore
// needs its own Lambda resource, backed by the same Redirector project which dispatches on the route.
```

The `qrStatusLambda` declaration (currently lines 50-54) stays as-is — the Aspire.Hosting.AWS wrapper does constrain one route per Lambda resource, so the two-resource pattern is correct.

- [ ] **Step 2: Build the AppHost**

Run: `dotnet build playground/lambda/LocalStack.Lambda.AppHost/LocalStack.Lambda.AppHost.csproj`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add playground/lambda/LocalStack.Lambda.AppHost/Program.cs
git commit -m "chore: remove misleading API Gateway emulator comment"
```

---

## Task 3: Relocate DynamoDbLocalGuardAnnotation to Annotations folder

**Files:**
- Create: `src/Aspire.Hosting.LocalStack/Annotations/DynamoDbLocalGuardAnnotation.cs`
- Modify: `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs:311, 316, 337`

**Interfaces:**
- Consumes: `Aspire.Hosting.ApplicationModel.IResourceAnnotation` (existing Aspire contract).
- Produces: `Aspire.Hosting.LocalStack.Annotations.DynamoDbLocalGuardAnnotation` — internal sealed marker class, drop-in replacement for the existing private nested class.

- [ ] **Step 1: Create the new annotation file**

Create `src/Aspire.Hosting.LocalStack/Annotations/DynamoDbLocalGuardAnnotation.cs`:
```csharp
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Annotations;

/// <summary>
/// Marker annotation that subscribes the DynamoDB Local fail-fast guard to a LocalStack resource.
/// Added once per LocalStack resource; the guard scans the final app model on BeforeStartEvent
/// and throws if AddAWSDynamoDBLocal is present alongside UseLocalStack().
/// </summary>
internal sealed class DynamoDbLocalGuardAnnotation : IResourceAnnotation;
```

- [ ] **Step 2: Update LocalStackResourceBuilderExtensions.cs to use the new type**

At the top of `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs`, add the using directive alongside the existing `Annotations` usings:
```csharp
using Aspire.Hosting.LocalStack.Annotations;
```

Replace the existing guard subscription (around line 311):
```csharp
if (localStack.Annotations.Any(annotation => annotation is DynamoDbLocalGuardAnnotation))
```
stays unchanged (the type name resolves to the new file via the using directive).

Replace the constructor call (around line 316):
```csharp
localStack.Annotations.Add(new DynamoDbLocalGuardAnnotation());
```
stays unchanged (same reason).

Delete the private nested class declaration at line 337:
```csharp
    private sealed class DynamoDbLocalGuardAnnotation : IResourceAnnotation;
```

- [ ] **Step 3: Build the package source**

Run: `dotnet build src/Aspire.Hosting.LocalStack/Aspire.Hosting.LocalStack.csproj`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 4: Run unit tests covering the annotation and the guard**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: all tests pass; total tests > 0; no `DynamoDbLocalGuardAnnotation`-related failures.

- [ ] **Step 5: Commit**

```bash
git add src/Aspire.Hosting.LocalStack/Annotations/DynamoDbLocalGuardAnnotation.cs src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs
git commit -m "refactor: move DynamoDbLocalGuardAnnotation to Annotations folder"
```

---

## Task 4: Extract CreateExecutableResourceByTypeName into shared test helper

**Files:**
- Create: `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestInfrastructure/TestResourceFactory.cs`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs:196, 240-252`
- Modify: `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs:108, 136-148`

**Interfaces:**
- Consumes: `Aspire.Hosting.ApplicationModel.ExecutableResource`, `Aspire.Hosting.AWS` (loaded into AppDomain).
- Produces: `Aspire.Hosting.LocalStack.Unit.Tests.TestInfrastructure.TestResourceFactory.CreateExecutableResourceByTypeName(string typeName, string name) → ExecutableResource`.

- [ ] **Step 1: Create the shared helper**

Create `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestInfrastructure/TestResourceFactory.cs`:
```csharp
using System.Reflection;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Unit.Tests.TestInfrastructure;

/// <summary>
/// Creates Aspire.Hosting.AWS helper resources (e.g., DynamoDB Streams event sources)
/// by type name. Reflection-based discovery mirrors ConstantsTests: type discovery must
/// not depend on whether another test already forced Aspire.Hosting.AWS into the AppDomain.
/// </summary>
internal static class TestResourceFactory
{
    public static ExecutableResource CreateExecutableResourceByTypeName(string typeName, string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                       .FirstOrDefault(type => type is not null)
                   ?? Assembly.Load("Aspire.Hosting.AWS").GetType(typeName, throwOnError: false)
                   ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the current assembly context.");

        return (ExecutableResource)(Activator.CreateInstance(type, name)
                                    ?? throw new InvalidOperationException($"Type '{typeName}' could not be created."));
    }
}
```

- [ ] **Step 2: Update UseLocalStackTests.cs to use the helper**

At the top of `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs`, add the using directive:
```csharp
using Aspire.Hosting.LocalStack.Unit.Tests.TestInfrastructure;
```

At line 196, replace:
```csharp
builder.AddResource(CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper"));
```
with:
```csharp
builder.AddResource(TestResourceFactory.CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper"));
```

Delete the local method declaration (lines 240-252):
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

- [ ] **Step 3: Update LocalStackConnectionStringAvailableCallbackTests.cs to use the helper**

At the top of `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs`, add the using directive:
```csharp
using Aspire.Hosting.LocalStack.Unit.Tests.TestInfrastructure;
```

At line 108, replace:
```csharp
var helperResource = CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper");
```
with:
```csharp
var helperResource = TestResourceFactory.CreateExecutableResourceByTypeName(Constants.DynamoDbStreamsEventSourceResource, "ddb-streams-helper");
```

Delete the local method declaration (lines 136-148) — same body as above.

- [ ] **Step 4: Run unit tests to confirm both call sites still work**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: all tests pass; total tests > 0; no test method removal (the test methods stay; only the helper moves).

- [ ] **Step 5: Commit**

```bash
git add tests/Aspire.Hosting.LocalStack.Unit.Tests/TestInfrastructure/TestResourceFactory.cs \
        tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs \
        tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs
git commit -m "refactor: extract CreateExecutableResourceByTypeName into shared test helper"
```

---

## Task 5: Refactor DynamoDB Local fail-fast test to ad-hoc DistributedApplicationTestingBuilder

**Files:**
- Delete: `tests/Aspire.Hosting.LocalStack.Integration.Tests/Playground/Lambda/DynamoDbLocalFailFastTests.cs`
- Create: `tests/Aspire.Hosting.LocalStack.Integration.Tests/LocalStack/DynamoDbLocalFailFastTests.cs`

**Interfaces:**
- Consumes: `DistributedApplicationTestingBuilder` (from `Aspire.Hosting.Testing`), `AddLocalStack`, `UseLocalStack`, `AddAWSDynamoDBLocal` extension methods.
- Produces: a test that exercises the exact same `BeforeStartEvent` guard without depending on `Projects.LocalStack_Lambda_AppHost`.

- [ ] **Step 1: Write the new test file**

Create `tests/Aspire.Hosting.LocalStack.Integration.Tests/LocalStack/DynamoDbLocalFailFastTests.cs`:
```csharp
using Aspire.Hosting;
using Aspire.Hosting.LocalStack;
using Aspire.Hosting.Testing;

namespace Aspire.Hosting.LocalStack.Integration.Tests.LocalStack;

/// <summary>
/// Guards the DynamoDB Local fail-fast: UseLocalStack() must reject AddAWSDynamoDBLocal
/// because DynamoDB Local and LocalStack's DynamoDB are competing backends and combining
/// them would split DynamoDB state. Uses the ad-hoc DistributedApplicationTestingBuilder.Create()
/// pattern so the test does not pull in the Lambda playground dependency graph.
/// </summary>
public class DynamoDbLocalFailFastTests
{
    [Test]
    public async Task UseLocalStack_Should_Fail_Fast_When_DynamoDb_Local_Is_Registered(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

        var builder = DistributedApplicationTestingBuilder.Create(["LocalStack:UseLocalStack=true"]);

        var localStack = builder.AddLocalStack("localstack");
        builder.UseLocalStack(localStack);
        builder.AddAWSDynamoDBLocal("dynamodb-local");

        await using var app = await builder.BuildAsync(cts.Token);

        await Assert.That(async () => await app.StartAsync(cts.Token))
            .ThrowsExactly<DistributedApplicationException>();
    }
}
```

- [ ] **Step 2: Delete the old test file**

```bash
git rm tests/Aspire.Hosting.LocalStack.Integration.Tests/Playground/Lambda/DynamoDbLocalFailFastTests.cs
```

- [ ] **Step 3: Run the new test**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Integration.Tests/Aspire.Hosting.LocalStack.Integration.Tests.csproj --treenode-filter "/*/*/*/*/*/Aspire.Hosting.LocalStack.Integration.Tests.LocalStack.DynamoDbLocalFailFastTests"`
Expected: 1 test passed. If the filter syntax fails, fall back to running the full integration test suite and confirm `UseLocalStack_Should_Fail_Fast_When_DynamoDb_Local_Is_Registered` shows as passed in the output.

If the test does not pass, investigate. Likely failure modes:
- `DistributedApplicationTestingBuilder.Create` requires `Aspire.Hosting.Testing` package reference (already in CPM, verify).
- The guard's `BeforeStartEvent` hook may need the LocalStack resource to be created before `UseLocalStack()` is called (already handled).

- [ ] **Step 4: Commit**

```bash
git add tests/Aspire.Hosting.LocalStack.Integration.Tests/LocalStack/DynamoDbLocalFailFastTests.cs
git commit -m "test: switch DynamoDB Local fail-fast test to ad-hoc DistributedApplicationTestingBuilder"
```

---

## Task 6: Switch S3UrlService real-AWS branch to presigned URLs

**Files:**
- Modify: `playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs`
- Create: `playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj` adjustment if test project setup needs it.
- Create: `playground/lambda/LocalStack.Lambda.Redirector.Tests/S3UrlServiceTests.cs` (new test project)

**Interfaces:**
- Consumes: `IAmazonS3`, `IOptions<LocalStack.Client.Options.LocalStackOptions>`.
- Produces: `IS3UrlService.GetS3Url(IAmazonS3 amazonS3, string bucket, string key)` — same signature; real-AWS branch returns presigned URL instead of hand-built URL.

- [ ] **Step 1: Make IS3UrlService and S3UrlService public, add Redirector project reference**

The current `S3UrlService` is `internal sealed` with explicit interface implementation (`string IS3UrlService.GetS3Url(...)`). For unit tests to call `GetS3Url` directly on the concrete type, make both `public` and switch the implementation to implicit.

In `playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs`:
- Change `internal sealed class S3UrlService : IS3UrlService` → `public sealed class S3UrlService : IS3UrlService`
- Change `internal interface IS3UrlService` → `public interface IS3UrlService`
- Change `string IS3UrlService.GetS3Url(...)` → `public string GetS3Url(...)`

This is playground code; no public-API compatibility concern.

In `tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`, add a project reference to the Redirector so the test project can see the now-public types:
```xml
<ProjectReference Include="$(MSBuildThisFileDirectory)../../../playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj" />
```

- [ ] **Step 2: Write a failing test for the presigned URL behavior**

Create the test file:
```csharp
using Amazon.S3;
using Amazon.S3.Model;
using LocalStack.Client.Options;
using LocalStack.Lambda.Redirector;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Playground.Redirector;

public class S3UrlServiceTests
{
    [Test]
    public async Task GetS3Url_RealAWS_ReturnsPresignedUrl()
    {
        // Arrange - LocalStackOptions with UseLocalStack = false
        var options = Microsoft.Extensions.Options.Options.Create(new LocalStackOptions().WithUseLocalStack(false));
        var service = new S3UrlService(options);
        var s3Client = Substitute.For<IAmazonS3>();

        s3Client.Config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.EUWest1 };
        s3Client.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("https://example.s3.eu-west-1.amazonaws.com/test-key?signature=abc");

        // Act
        var url = service.GetS3Url(s3Client, "example", "test-key");

        // Assert
        await Assert.That(url).StartsWith("https://");
        await Assert.That(url).Contains("signature=");
        await s3Client.Received(1).GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>());
    }

    [Test]
    public async Task GetS3Url_LocalStack_ReturnsDirectEdgeUrl()
    {
        // Arrange
        var localStackOptions = new LocalStackOptions()
            .WithUseLocalStack(true);
        var options = Microsoft.Extensions.Options.Options.Create(localStackOptions);
        var service = new S3UrlService(options);
        var s3Client = Substitute.For<IAmazonS3>();

        // Act
        var url = service.GetS3Url(s3Client, "mybucket", "mykey");

        // Assert - direct LocalStack edge URL, no presign
        await Assert.That(url).Contains("localhost");
        await Assert.That(url).Contains("/mybucket/mykey");
        await s3Client.DidNotReceive().GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>());
    }
}
```

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: FAIL on the presigned URL test (current implementation returns hand-built URL).

- [ ] **Step 3: Implement the presigned URL change**

Replace the real-AWS branch in `playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs`:
```csharp
string IS3UrlService.GetS3Url(IAmazonS3 amazonS3, string bucket, string key)
{
    if (_localStackOptions.UseLocalStack)
    {
        return $"http://{_localStackOptions.Config.LocalStackHost}:{_localStackOptions.Config.EdgePort}/{bucket}/{key}";
    }

    var request = new GetPreSignedUrlRequest
    {
        BucketName = bucket,
        Key = key,
        Expires = DateTime.UtcNow.AddHours(1)
    };

    return amazonS3.GetPreSignedURL(request);
}
```

Add the using directive at the top:
```csharp
using Amazon.S3.Model;
```

Remove the now-unused `Environment.GetEnvironmentVariable` region parsing and the manual URL builder. The `AmazonS3Config` and `AWS_REGION` lookup code is no longer needed — `GetPreSignedURL` reads region from the client's config.

- [ ] **Step 4: Run the test again**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: PASS for both tests.

- [ ] **Step 5: Build the Redirector and the AppHost**

Run: `dotnet build playground/lambda/LocalStack.Lambda.Redirector/LocalStack.Lambda.Redirector.csproj`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **Step 6: Commit**

```bash
git add playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs \
        playground/lambda/LocalStack.Lambda.Redirector.Tests/ \
        tests/Aspire.Hosting.LocalStack.Unit.Tests/Playground/Redirector/
git commit -m "fix(playground): use presigned URLs for real-AWS S3 object URLs"
```

---

## Task 7: Bump .NET SDK pin to 10.0.301

**Files:**
- Modify: `global.json`

**Interfaces:**
- Consumes: n/a.
- Produces: SDK pin aligned with locally installed `10.0.301`.

- [ ] **Step 1: Update global.json**

Replace the contents of `global.json`:
```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 2: Restore the solution**

Run: `dotnet restore LocalStack.sln`
Expected: Restore completed successfully, no NU warnings.

- [ ] **Step 3: Build the full solution**

Run: `dotnet build LocalStack.sln`
Expected: Build succeeded, 0 errors, 0 warnings. Analyzers (`Meziantou`, `Roslynator`, `Sonar`, `BannedApi`) all pass.

- [ ] **Step 4: Run unit tests**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: all tests pass; total > 0.

- [ ] **Step 5: Commit**

```bash
git add global.json
git commit -m "chore: bump .NET SDK pin to 10.0.301"
```

---

## Task 8: Migrate solution to slnx format

**Files:**
- Create: `LocalStack.slnx`
- Delete: `LocalStack.sln`

**Interfaces:**
- Consumes: `dotnet sln migrate` (requires .NET SDK 10+).
- Produces: `LocalStack.slnx` — XML-based solution file without `Debug|x64`/`Debug|x86` platform configurations.

- [ ] **Step 1: Run the migration**

Run: `dotnet sln LocalStack.sln migrate`
Expected: `LocalStack.slnx` is created alongside `LocalStack.sln`.

- [ ] **Step 2: Inspect the new slnx for platform configurations**

Open `LocalStack.slnx` in an editor. Verify it does not contain `Debug|x64`, `Debug|x86`, `Release|x64`, or `Release|x86` configurations. The slnx format typically uses only `Debug|Any CPU` and `Release|Any CPU` by default.

If `x64`/`x86` configurations somehow appear, delete them.

- [ ] **Step 3: Delete the old sln file**

```bash
git rm LocalStack.sln
```

- [ ] **Step 4: Build the solution using the new slnx**

Run: `dotnet build LocalStack.slnx`
Expected: Build succeeded, 0 errors. Same project set as the previous sln.

- [ ] **Step 5: Verify IDE/tooling can open slnx**

Open Rider (or VS Code with the C# Dev Kit) and confirm `LocalStack.slnx` loads all projects. If your IDE cannot open slnx yet, document the workaround in the PR description.

- [ ] **Step 6: Commit**

```bash
git add LocalStack.slnx
git commit -m "build: migrate solution to slnx format, drop x64/x86 platform configs"
```

---

## Task 9: Commit playground aspire.config.json

**Files:**
- Modify: `.gitignore` (remove line 432)
- Add: `playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json` (already on disk)

**Interfaces:**
- Consumes: n/a.
- Produces: a shared `aspire.config.json` for the Lambda playground AppHost that lets the Aspire CLI skip filesystem scanning.

- [ ] **Step 1: Remove the gitignore entry**

Open `.gitignore` and delete the line:
```
playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json
```
(line 432).

- [ ] **Step 2: Verify the file content is safe to commit**

Run: `cat playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json`
Expected content:
```json
{
  "appHost": {
    "path": "LocalStack.Lambda.AppHost.csproj"
  }
}
```
Verify: relative path only, no machine-specific data, no secrets.

- [ ] **Step 3: Add and commit**

```bash
git add .gitignore playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json
git commit -m "chore: commit playground aspire.config.json"
```

- [ ] **Step 4: Run Aspire CLI to verify the file is picked up**

Run: `aspire ps` from the repo root, or `aspire start` in the Lambda playground directory (do not actually start if integration tests already run; just verify discovery).
Expected: CLI uses the configured AppHost path without filesystem scanning.

---

## Final Validation

After all nine tasks:

- [ ] **Full solution build**

Run: `dotnet build LocalStack.slnx`
Expected: Build succeeded, 0 errors, 0 warnings.

- [ ] **All unit tests pass**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj`
Expected: all tests pass; total > 0 (target: 486+ across 3 TFMs, plus new S3UrlService tests).

- [ ] **All integration tests pass (Docker required)**

Run: `dotnet test --project tests/Aspire.Hosting.LocalStack.Integration.Tests/Aspire.Hosting.LocalStack.Integration.Tests.csproj`
Expected: all tests pass including the refactored `DynamoDbLocalFailFastTests` (now in `LocalStack/` namespace).

- [ ] **Slopwatch scan (if available)**

Run: `slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"`
Expected: 0 warnings.

- [ ] **Open PR**

Push branch, open PR against the base branch (likely `main`). Title:
```
chore: post-review followups from 02d386278e
```
Body uses bullets matching the nine tasks above. Use **squash merge**.

## Spec Coverage Self-Review

Spec requirements → task mapping:

| Spec section | Task |
| --- | --- |
| Docs and comments / 1. CONFIGURATION.md and README.md | Task 1 |
| Docs and comments / 2. Program.cs comment | Task 2 |
| Code refactor / 3. DynamoDbLocalGuardAnnotation relocation | Task 3 |
| Code refactor / 4. CreateExecutableResourceByTypeName extraction | Task 4 |
| Behavior change / 5. S3UrlService presigned URLs | Task 6 |
| Behavior change / 6. DynamoDB Local fail-fast test refactor | Task 5 |
| Build / 7. SDK bump | Task 7 |
| Build / 8. slnx migration | Task 8 |
| Build / 9. aspire.config.json commit | Task 9 |

All spec sections have a corresponding task. No placeholders. Type names (`DynamoDbLocalGuardAnnotation`, `TestResourceFactory`, `IS3UrlService`) are consistent across tasks.
