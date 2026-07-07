# Post-review Followups from `02d386278e`

Date: 2026-07-07

## Context

After committing `02d386278e` (feat(ws2): DynamoDB Streams event sources on LocalStack with async QR playground), a self-review surfaced a list of out-of-order notes spanning docs, refactor, behavior, build, and test concerns. This spec captures the agreed scope and approach for resolving them in a single PR on the current `feature/ws2-dynamodb-streams-squashed` branch.

Two research questions were resolved during brainstorming:

1. **`aspire.config.json` purpose.** Local `aspire.config.json` is officially supported by the Aspire CLI (13.3+). It lets the CLI skip filesystem scanning for AppHost discovery and use the configured path directly. The playground file contains a relative `appHost.path` (no machine-specific data), so it is safe to commit.
2. **On-the-fly AppHost for integration tests.** `DistributedApplicationTestingBuilder.Create()` (non-generic) creates a `DistributedApplicationBuilder` directly in the test process without requiring any AppHost project. Aspire's own `Aspire.Hosting.Testing.Tests/TestingBuilderTests.cs` uses this pattern. This is the recommended approach for the DynamoDB Local fail-fast test.

A separate finding from research: the API Gateway emulator (Lambda TestTool) **does** support multiple routes per Lambda resource — it scans all `APIGATEWAY_EMULATOR_ROUTE_CONFIG*` environment variables. The one-route-per-Lambda constraint is in the `Aspire.Hosting.AWS` wrapper (`APIGatewayExtensions.WithReference`), which keys the env var suffix on `lambda.Resource.Name`. The existing comment in `LocalStack.Lambda.AppHost/Program.cs` is misleading because it blames the emulator.

## Branch and Merge Strategy

- Work continues on the current branch: `feature/ws2-dynamodb-streams-squashed`.
- Single PR, **squash merge**. The squash commit message follows the Conventional Commit format:
  ```
  chore: post-review followups from 02d386278e
  ```
  with a bulleted body covering each concern.
- Sub-commits during development are free-form (useful for diff review); squash normalizes history.

## Scope

Nine changes across six concerns.

### Docs and comments (zero risk)

1. **`docs/CONFIGURATION.md` and `README.md`** — replace hardcoded image-tag versions with placeholders.
   - `docs/CONFIGURATION.md`: ten locations reference `4.12.0` as the default tag or as example values. Replace with formulation like `null (current LocalStack tag)` for defaults, and `e.g. "4.x.x"` for examples. Keep historical entries in `CHANGELOG.md` (sections `[13.1.0]` and `[9.5.3]`) untouched — those are version notes, not living defaults.
   - `README.md:123`: `ContainerImageTag - Custom image tag/version (default: 4.12.0)` → drop the hardcoded default, use a placeholder formulation.
2. **`playground/lambda/LocalStack.Lambda.AppHost/Program.cs:47-49`** — delete the misleading comment that blames the API Gateway emulator for the one-route-per-Lambda constraint. The code (two Lambda resources backed by the same Redirector project that dispatches on route path) is self-explanatory once the misleading framing is removed.

### Code refactor (low risk)

3. **`DynamoDbLocalGuardAnnotation` relocation.** Currently a private nested class at `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs:337`. Move to `src/Aspire.Hosting.LocalStack/Annotations/DynamoDbLocalGuardAnnotation.cs` as an `internal sealed` class, matching the layout of the other two annotations (`LocalStackReferenceAnnotation`, `LocalStackEnabledAnnotation`).
4. **`CreateExecutableResourceByTypeName` extraction.** Currently duplicated verbatim in two test files:
   - `tests/Aspire.Hosting.LocalStack.Unit.Tests/Extensions/ResourceBuilderExtensionsTests/UseLocalStackTests.cs:240`
   - `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs:136`

   Extract to a shared static helper at `tests/Aspire.Hosting.LocalStack.Unit.Tests/TestInfrastructure/TestResourceFactory.cs`. Both call sites switch to the shared helper. `[UnsafeAccessor]` is **not** applicable here — the target type and constructor are public; this is reflection-based type discovery, not private-member access.

### Behavior change (medium risk)

5. **`S3UrlService` real-AWS branch.** Currently builds S3 object URLs by hand at `playground/lambda/LocalStack.Lambda.Redirector/S3UrlService.cs:23-31`:
   ```csharp
   return $"https://{bucket}.s3.{awsRegion}.amazonaws.com/{key}";
   ```
   This works only for public objects. Switch to `AmazonS3Client.GetPreSignedURL()` with a reasonable expiry (1 hour default) for the real-AWS branch. The LocalStack branch is unchanged — presigned URLs in proxy-mode LocalStack.Client leak the AWS regional host (Known Issue documented in `02d386278e`). The `IS3UrlService` interface signature does not change.
6. **DynamoDB Local fail-fast test refactor.** `tests/Aspire.Hosting.LocalStack.Integration.Tests/Playground/Lambda/DynamoDbLocalFailFastTests.cs` currently references `Projects.LocalStack_Lambda_AppHost`, pulling in the full Lambda playground dependency graph. Switch to the ad-hoc `DistributedApplicationTestingBuilder.Create()` pattern:
   ```csharp
   var builder = DistributedApplicationTestingBuilder.Create([]);
   var localStack = builder.AddLocalStack("localstack");
   builder.UseLocalStack(localStack);
   builder.AddAWSDynamoDBLocal("dynamodb-local");
   await using var app = await builder.BuildAsync();
   await Assert.That(async () => await app.StartAsync())
       .ThrowsExactly<DistributedApplicationException>();
   ```
   This tests the exact same guard logic (BeforeStart event subscription) without the playground coupling. The test file moves from `tests/Aspire.Hosting.LocalStack.Integration.Tests/Playground/Lambda/DynamoDbLocalFailFastTests.cs` to `tests/Aspire.Hosting.LocalStack.Integration.Tests/LocalStack/DynamoDbLocalFailFastTests.cs` since it no longer depends on the Lambda playground.

### Build (low risk)

7. **`global.json` SDK bump.** `10.0.100` → `10.0.301`. `rollForward: latestFeature` already permits the bump at runtime; this just makes the pin explicit. Verify with full restore/build/test cycle.
8. **Solution format migration.** `LocalStack.sln` → `LocalStack.slnx` via `dotnet sln migrate`. Drop `Debug|x64` and `Debug|x86` platform configurations (they map to `Any CPU` anyway). Verify IDEs (Rider, VS Code, VS) and CI can open the new format.
9. **Commit `aspire.config.json`.** Remove line 432 from `.gitignore` (`playground/lambda/LocalStack.Lambda.AppHost/aspire.config.json`) and commit the existing file. Content is `{"appHost": {"path": "LocalStack.Lambda.AppHost.csproj"}}` — relative path, not machine-specific.

## Validation Gates

- After each behavior/refactor change: `dotnet build` and TUnit unit tests (`dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/...`; confirm total > 0; avoid plain `--filter`).
- After build changes (SDK bump, slnx migration): `dotnet restore` + full solution build.
- Integration tests requiring Docker (including the new ad-hoc DynamoDB Local test): run on Deniz's machine before PR.
- Doc changes: visual link/typo check; no build needed.

## Out of Scope

- Package version bumps beyond the SDK pin (deferred).
- `CHANGELOG.md` Known Limitations additions (optional follow-up; current Known Issues section already covers the relevant items).
- Upstream Aspire.Hosting.AWS issue for env var suffix uniqueness — separate task.
- General documentation quality audit beyond version placeholder cleanup.
- Refactoring `Redirector` to a single Lambda resource (blocked by Aspire.Hosting.AWS wrapper API; left as-is).

## Open Questions Resolved During Brainstorming

- Q: Image tag documentation approach? **A:** Placeholder + `e.g.` style; no hardcoded version anywhere outside CHANGELOG history.
- Q: API Gateway comment? **A:** Delete (the comment is misleading; the constraint is in the wrapper, not the emulator).
- Q: S3 real-AWS branch? **A:** Switch to `GetPreSignedURL()`.
- Q: Annotation location? **A:** Move to `Annotations/` folder.
- Q: Test helper dedup? **A:** Shared static helper; `[UnsafeAccessor]` not applicable.
- Q: Known Limitations location? **A:** Keep in README; CHANGELOG already has Known Issues.
- Q: SDK bump? **A:** Yes, to 10.0.301.
- Q: slnx migration? **A:** Yes; also drop x64/x86 configs.
- Q: aspire.config.json? **A:** Commit (relative path, safe).
- Q: Package updates? **A:** Skip for now.
- Q: On-the-fly AppHost integration test? **A:** Yes, via ad-hoc `Create()` pattern.
- Q: Doc QA scope? **A:** Version placeholders only.
