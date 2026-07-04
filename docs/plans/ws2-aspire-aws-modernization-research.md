# WS2 Aspire/AWS Modernization Research

Date: 2026-07-02 (re-validated same day: adversarial verification pass against `external/` checkouts, GitHub releases, and web sources; corrections from that pass are folded in below)

## Scope

This document captures the WS2 research pass for Aspire core and `Aspire.Hosting.AWS` modernization after the package update wave. It is a research result and decision input, not an implementation plan.

The goal is to decide which new upstream Aspire/AWS integration capabilities should be adapted for LocalStack and which changes affect this package's compatibility-sensitive implementation.

## Inputs Checked

### Package Versions

Versions were read from `Directory.Packages.props`:

| Package | Version |
| --- | --- |
| `Aspire.Hosting` | `13.4.6` |
| `Aspire.Hosting.AppHost` | `13.4.6` |
| `Aspire.Hosting.Testing` | `13.4.6` |
| `Aspire.Hosting.AWS` | `13.3.1` |
| `LocalStack.Client` | `2.0.0` |
| `LocalStack.Client.Extensions` | `2.0.0` |

### Local Upstream Source

The `external/` tree is gitignored. It was checked with ignored-file-aware commands and direct reads, not normal workspace globbing.

| Source | Local path | Verified ref |
| --- | --- | --- |
| Aspire current | `external/aspire/v13.4.6/` | `v13.4.6`, `87fe259e4fc244c599019a7b1304c85a1488f248` |
| Aspire comparison | `external/aspire/v13.1.0/` | `v13.1.0`, `8a4db1775c3fbae1c602022b636299cb04971fde` |
| AWS integration current | `external/aws-integrations/release_2026-06-19/` | `release_2026-06-19`, `b8f5c040b2a41a655ab9ae4d2c528803d5066ae6` |
| AWS integration comparison | `external/aws-integrations/release_2025-10-24/` | `release_2025-10-24`, `83e36c3e76a0d4e3846eaae411d6af2734d1af49` |
| LocalStack client | `external/localstack-dotnet-client/v2.0.0/` | `v2.0.0`, `873efbfbdfb43abe89df2b2e6458dbf90c0a6a92` |

The AWS integration release tags map to package versions through `src/Aspire.Hosting.AWS/Aspire.Hosting.AWS.csproj`:

| Release tag | `Aspire.Hosting.AWS` version |
| --- | --- |
| `release_2025-10-24` | `9.3.0` |
| `release_2026-06-19` | `13.3.1` |

As of 2026-07-02, `release_2026-06-19` (AWS integrations), `v13.4.6` (Aspire), and `v2.0.0` (LocalStack.Client) are still the latest upstream releases, so the deltas below are complete. The Aspire repository now lives at `microsoft/aspire`; old `dotnet/aspire` URLs redirect.

### External Documentation And Web Sources

| Source | URL | Result |
| --- | --- | --- |
| Aspire 13.4 what's new | `https://aspire.dev/whats-new/aspire-13-4/` | Checked. Main relevant items: TypeScript AppHost/ATS GA, resource command improvements, `WithHidden`, persistent executable/project lifetimes, testing HTTPS preference note. |
| Aspire 13.4.0 release | `https://github.com/dotnet/aspire/releases/tag/v13.4.0` | Checked. Confirms 13.4 themes and links to full docs. |
| Aspire 13.4.6 release | `https://github.com/dotnet/aspire/releases/tag/v13.4.6` | Checked. Patch release: polyglot binding, isolated-mode resource service port collision, MongoDB dependency. |
| AWS integration release 2026-06-19 | `https://github.com/aws/integrations-on-dotnet-aspire-for-aws/releases/tag/release_2026-06-19` | Checked. Confirms `13.3.1`, AgentCore experimental warning placement, and `BeforeStartEvent` endpoint injection. |
| AWS integration release 2025-10-24 | `https://github.com/aws/integrations-on-dotnet-aspire-for-aws/releases/tag/release_2025-10-24` | Checked. Confirms `9.3.0` and `AddAWSDynamoDBLocal` return-type breaking change. |
| AWS integration CHANGELOG | `https://raw.githubusercontent.com/aws/integrations-on-dotnet-aspire-for-aws/main/CHANGELOG.md` | Checked. Used as feature delta source from `9.3.0` to `13.3.1`. Note: the CHANGELOG at the `release_2026-06-19` tag lacks the `13.3.1` entry itself — use `main` or the GitHub release body for `13.3.1`. The `13.2.0` S3-assets bullet appears only in the CHANGELOG, not that release's body. |
| AWS integration README | `https://raw.githubusercontent.com/aws/integrations-on-dotnet-aspire-for-aws/release_2026-06-19/src/Aspire.Hosting.AWS/README.md` | Checked. Used for public feature descriptions. |
| AWS SDK service-specific endpoints | `https://docs.aws.amazon.com/sdkref/latest/guide/feature-ss-endpoints.html` | Checked. Confirms `AWS_ENDPOINT_URL_<SERVICE>` overrides global `AWS_ENDPOINT_URL`, both supported by AWS SDK for .NET v3/v4. |
| AWS Developer Tools Blog search | `https://aws.amazon.com/blogs/developer/?s=.NET+Aspire+AWS` and similar | Checked; re-verified 2026-07-02. No dedicated `Aspire.Hosting.AWS` announcement exists in the window. Notable adjacent post: AWS SDK for .NET V3 end-of-support (V3 in maintenance mode since 2026-03-01; this repo is on 4.x). |
| AWS SDK endpoint identifiers | `https://docs.aws.amazon.com/sdkref/latest/guide/ss-endpoints-table.html` | Checked. DynamoDB Streams is a separate endpoint identifier (`AWS_ENDPOINT_URL_DYNAMODB_STREAMS`, config key `dynamodb_streams`) from DynamoDB (`AWS_ENDPOINT_URL_DYNAMODB`). |
| LocalStack DynamoDB Streams docs | `https://docs.localstack.cloud/aws/services/dynamodbstreams/` | Checked. Included in all plans (Hobby/Base/Ultimate); stream persistence not supported across restarts. |
| LocalStack single-image transition | `https://blog.localstack.cloud/localstack-single-image-next-steps/` | Checked. Since 2026-03-23 LocalStack ships one unified image; new releases require an auth token including in CI; a free Hobby plan exists; older pinned tags remain token-free. |

## Aspire Core Findings

### Compatibility Conclusion

No WS2 production-code change is needed for Aspire core. The `13.1.0` to `13.4.6` changes do not break this package's current use of Aspire hosting APIs.

### Source Areas Checked

| Area | Files/symbols checked | Impact |
| --- | --- | --- |
| Resource interfaces | `IResourceWithEnvironment`, `IResourceWithEndpoints`, `IResourceWithConnectionString`, `IResourceWithWaitSupport`, `IResourceWithServiceDiscovery` | Current `LocalStackResource` shape remains compatible. |
| Reference wiring | `ResourceBuilderExtensions.WithReference(...)`, `ReferenceEnvironmentInjectionAnnotation`, `ConnectionStringReference` | Current `.WithReference(localStack)` connection-string path remains compatible. |
| Environment wiring | `ResourceBuilderExtensions.WithEnvironment(...)`, `EnvironmentCallbackAnnotation`, `EnvironmentCallbackContext` | Current env-var callbacks remain compatible. |
| Endpoint model | `EndpointReference`, `EndpointReferenceExpression`, `GetEndpoint(...)`, endpoint properties | Current use of `GetEndpoint("http")`, `EndpointProperty.Host`, and `EndpointProperty.Port` remains compatible. |
| Wait model | `WaitFor`, `WaitForCompletion`, `WaitAnnotation`, `IResourceWithWaitSupport` | Current CDK bootstrap ordering remains compatible. |
| Manifest exclusion | `ExcludeFromManifest`, `ManifestPublishingCallbackAnnotation.Ignore` | Current LocalStack/CDK bootstrap manifest exclusion remains compatible. |
| Testing builder | `DistributedApplicationHostingTestingExtensions.CreateHttpClient`, `GetEndpointUriStringCore` | Endpoint-name omission now prefers `https`; this repo passes `"http"` explicitly in integration tests. |

### New Aspire Core Capabilities Relevant Later

| Capability | Source | WS2 impact | Later use |
| --- | --- | --- | --- |
| `IResourceWithCustomWithReference<TSelf>` | `src/Aspire.Hosting/ApplicationModel/IResourceWithCustomWithReference.cs` | No immediate action. Verified: its dispatcher is only invoked from the internal object-typed `WithReference` used by polyglot/ATS app hosts; the public strongly-typed C# `WithReference` overloads never consult it. | Not usable by this package today; re-evaluate only if upstream wires it into the public C# path. |
| `WithHidden()` / `WithHiddenOnCompletion()` | `ResourceBuilderExtensions.cs` | No immediate action. | Could hide implementation-detail resources such as CDK bootstrap or event-source helper resources from dashboard/CLI views. |
| `WithProcessCommand()` | `ResourceBuilderExtensions.cs` | No immediate action. | Possible future UX feature for LocalStack dashboard/resource commands. |
| Persistent executable/project lifetimes | `WithPersistentLifetime()` | No immediate action. | Not a replacement for current LocalStack container lifetime behavior. |

### Verified Behavioral Deltas Not Affecting This Package

Found during re-validation by diffing `v13.1.0` against `v13.4.6`. None intersect this package's current code paths, but they matter for future features:

| Change | Source | Why it does not affect this package today |
| --- | --- | --- |
| Environment/args callbacks are evaluated once and cached until resource restart (`EvaluateOnceAsync`; invalidated on restart via `ForgetCachedCallbackResults`) | `EnvironmentCallbackAnnotation`, `Dcp/DcpExecutor.cs` | This package's callbacks set values that are fixed once the connection string exists. Future callbacks must not expect per-evaluation freshness. |
| Service-discovery env keys switched from endpoint-name-based (`services__{name}__{endpoint}__0`) to scheme-based keys with dynamic indices; endpoints marked `ExcludeReferenceEndpoint` are skipped | `ResourceBuilderExtensions.cs` | LocalStack is not `IResourceWithServiceDiscovery`; only consumer apps referencing projects see different keys. |
| `WithLifetime(ContainerLifetime)` now writes `PersistenceAnnotation` instead of `ContainerLifetimeAnnotation` (legacy annotation still read for back-compat) | `ContainerResourceBuilderExtensions.cs` | This package only calls the API and never inspects lifetime annotations. |
| `ConnectionStringAvailableEvent` publishing was rearchitected (dedicated DCP context; skipped until endpoints are allocated; explicit-start resources now fire at actual start instead of DCP object creation) | `Dcp/DcpExecutor.cs`, `Orchestrator/ApplicationOrchestrator.cs` | Ordering (before `BeforeResourceStartedEvent`) and once-per-(re)start multiplicity are unchanged for the LocalStack container's default lifecycle. |
| `IResourceWithConnectionString` base list now goes through `IExpressionValue` | `ApplicationModel/IResourceWithConnectionString.cs` | `IExpressionValue : IValueProvider, IManifestExpressionProvider` — the transitive member set is unchanged. |
| New `[Obsolete]` members: `EndpointAnnotation.AllocatedEndpointSnapshot`/`TryAdd`, `CommandOptions.Parameter`, `ResourceCommandAnnotation.Parameter`/`ErrorMessage` | Various | This package uses none of them. |

## Aspire.Hosting.AWS Findings

### Existing String-Matched Types

This repo currently depends on AWS integration internals by full type-name string:

| Constant | Upstream file | Current result |
| --- | --- | --- |
| `Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation` | `src/Aspire.Hosting.AWS/CloudFormation/CloudFormationReferenceAnnotation.cs` | Still exists in `13.3.1`; still `internal sealed class ... : IResourceAnnotation`; file byte-identical to `9.3.0`. |
| `Aspire.Hosting.AWS.Lambda.SQSEventSourceResource` | `src/Aspire.Hosting.AWS/Lambda/SQSEventSourceResource.cs` | Still exists in `13.3.1`; still `internal class ... : ExecutableResource`; file byte-identical to `9.3.0`. Type-name matching is safe. The `13.0.1` dedupe fix changed the helper's default *instance-name* pattern instead (see delta table) — only name-convention matching would break. |

Existing tests in `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/ConstantsTests.cs` correctly guard these names through reflection.

### AWS Integration Delta Since 9.3.0

| Version/release | Upstream change | LocalStack impact |
| --- | --- | --- |
| `9.3.1`, `9.3.2` | Dependency bumps (AWSSDK.Core, Lambda Test Tool 0.12.0, RuntimeSupport 1.14.2). | None. |
| `9.4.0` | Lambda/API Gateway emulator HTTPS support; `Port` renamed to `HttpPort`; `HttpsPort` and `DisableHttpsEndpoint` added. | Mostly independent. This repo's tests call explicit `"http"` endpoints. |
| `9.4.1` | Lambda HTTPS ports enabled by default only when an ASP.NET Core dev cert is present; the effective `DisableHttpsEndpoint` default is dev-cert-dependent. | None directly; emulator HTTPS availability varies per machine. |
| `13.0.0` | Lambda integration APIs removed preview flags; AWS publish/deploy preview added through `AddAWSCDKEnvironment`. | Lambda support remains valid. Publish/deploy targets real AWS and should not be adapted for LocalStack now. |
| `13.0.1` | SQS event-source resource-name dedupe fix: default helper instance name changed to `SQSEventSource-{lambda}-{queue}` (64-char cap with hash suffix) and `SQSEventSourceOptions.ResourceName` was added. | Existing string-match by type remains unaffected. |
| `13.0.2` | Lambda class-library wrapper-project `runtimeconfig.json` first-run fix. | None. |
| `13.1.0` | `WithDynamoDBStreamsEventSource` added for Lambda DynamoDB Streams emulation. Also publish/deploy work: `WithEnvironment` resolution for ECS Fargate/Lambda publish targets, Aspire parameters to CloudFormation parameters, stack deletion on `aspire destroy`. | The event source is the main new LocalStack-adaptable feature; the publish/deploy items target real AWS. |
| `13.2.0` | S3 assets supported when provisioning AWS app resources. | Existing CDK asset upload endpoint customizer remains important. |
| `13.3.0` | Experimental AgentCore local development support added. | Ignore for LocalStack: uses embedded in-process emulators from `AWS.AgentCore.Testing`. |
| `13.3.1` | AgentCore endpoint injection moved to `BeforeStartEvent` hook using stock `WithReference`. | Confirms a useful pattern, but not a LocalStack feature. |

### Other Upstream Changes Verified As Not Affecting This Package

Grep confirms this repo (`src/`, `tests/`, `playground/`) has zero references to any of these surfaces:

| Change | Notes |
| --- | --- |
| `LambdaEmulatorAnnotation.Endpoint` renamed to `LambdaRuntimeEndpoint` | Breaking only for consumers that reflect over that annotation's members. |
| Lambda/API Gateway emulators are now dual-endpoint (`"http"` = Lambda runtime API, `"https"` = Test Tool web UI) with new `LAMBDA_RUNTIME_API_PORT`, `LAMBDA_WEB_UI_HTTPS_PORT`, `API_GATEWAY_EMULATOR_HTTPS_PORT` env vars | Consumers that grabbed "the" single emulator endpoint must now ask for `"http"` by name; `AWS_LAMBDA_RUNTIME_API` still uses the http endpoint. |
| `[RequiresPreviewFeatures]` removed from the Lambda/SQS surface | Consumers with `EnablePreviewFeatures` shims can drop them; this repo had none. |
| Package now multi-targets `net8.0` + `net10.0`; AgentCore surface is `net10.0`-only | — |
| Lifecycle hooks replaced by eventing subscribers (`AWSLifecycleHook` → `AWSBeforeStartEventHandler`, `LambdaLifecycleHook` → `LambdaBeforeStartEventHandler`) | Relevant only if this package ever depends on ordering relative to AWS's startup work. |
| New publish-target annotation family (`IAWSPublishTargetAnnotation` + ECS Fargate/Lambda/ElastiCache implementations) and new `Aspire.Hosting.Redis`/`Aspire.Hosting.Valkey` package dependencies | Publish/deploy path, real AWS only. |

## Feature Decision Matrix

| Feature | Upstream status | Current repo support | Decision |
| --- | --- | --- | --- |
| CloudFormation templates/stacks | Stable existing feature | Supported through `UseLocalStack()` and `.WithReference(localStack)` | Keep current support. |
| CDK stacks/constructs | Stable existing feature | Supported with CDK bootstrap and LocalStack endpoint routing | Keep current support. |
| Lambda function emulator | Stable existing feature | Covered by integration tests/playground | Keep current support. |
| API Gateway emulator | Stable existing feature | Covered by Lambda fixture/test usage | Keep current support. |
| SQS event source | Stable existing feature | Supported through internal type-name matching | Keep current support and tests. |
| DynamoDB Local | Existing AWS integration feature | Not integrated directly; independent container resource | Defer direct integration. WS3 native endpoints should make mixed usage easier. |
| DynamoDB Streams event source | New since `13.1.0` | Not supported by this package yet | Support next, subject to design and tests. |
| AgentCore | Experimental, `net10.0`, in-process emulators | Not supported | Ignore for LocalStack for now. |
| AWS publish/deploy | Preview, real AWS CDK deployment target | Not supported | Ignore for LocalStack local-development package for now. |
| CDK S3 asset uploader | New asset upload path in provisioning | Customizer exists and is tested | Keep current support; monitor AWS SDK pipeline assumptions. |

## DynamoDB Streams Event Source Details

`WithDynamoDBStreamsEventSource` is the only clearly actionable WS2 feature found in this pass.

Upstream files checked:

| File | Finding |
| --- | --- |
| `src/Aspire.Hosting.AWS/Lambda/DynamoDBStreamsEventSourceResource.cs` | New internal `ExecutableResource`; full type name is `Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource`; command uses `lambda-test-tool start --no-launch-window --dynamodbstreams-eventsource-config env:DYNAMODB_STREAMS_EVENTSOURCE_CONFIG`. The config string carries `TableName`, `FunctionName`, `LambdaRuntimeApi` plus optional `BatchSize`, `PollingIntervalMs`, `Profile`, `Region`. |
| `src/Aspire.Hosting.AWS/Lambda/DynamoDBStreamsEventSourceExtensions.cs` | Adds helper resource with parent relationship and `ExcludeFromManifest`; resolves table name from string, CDK construct output, or CloudFormation output. |
| `src/Aspire.Hosting.AWS/Lambda/DynamoDBStreamsEventSourceExtensions.cs` | When a Lambda has `DynamoDBLocalInstance` (populated only by AWS's own `DynamoDBLocalResource` via `WithReference`), upstream emits both `AWS_ENDPOINT_URL_DYNAMODB` and `AWS_ENDPOINT_URL_DYNAMODB_STREAMS`. Without it — the LocalStack case — upstream emits no endpoint env vars at all, so the poller resolves endpoints from `Profile`/`Region` and targets real AWS. |
| `src/Aspire.Hosting.AWS/Lambda/SQSEventSourceResource.cs` | Existing SQS event-source pattern is very similar and already handled by this repo. |

### Verified Constraints For The LocalStack Adapter

| Constraint | Evidence | Consequence |
| --- | --- | --- |
| Today the helper falls through every `UseLocalStack()` branch | `LocalStackResourceBuilderExtensions.cs:103-111`: the type-name branch matches only SQS, and the relationship branch requires a CloudFormation parent, while the helper's parent relationship points at the Lambda project | The helper is silently ignored: no endpoint injection and no `WaitFor(localstack)` — it would start polling before LocalStack is healthy and target real AWS. Support must go through the `WithReference` attachment path, not env vars alone. |
| Service-specific endpoint vars are required, not optional | Upstream's own env callback sets `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS` in the DynamoDB-local case; service-specific vars beat the global `AWS_ENDPOINT_URL` in the AWS SDK precedence chain; DynamoDB Streams is a separate endpoint identifier from DynamoDB | A global-only configurator can be silently overridden in mixed scenarios. Emit `AWS_ENDPOINT_URL`, `AWS_ENDPOINT_URL_DYNAMODB`, and `AWS_ENDPOINT_URL_DYNAMODB_STREAMS`. |
| Env-callback ordering is last-writer-wins and implicit | Upstream's callback and this package's callback write the same env dictionary; this package's callback is annotated later (at connection-string-available time) and currently runs last | Works today, but untested — the adapter's tests should pin that this package's values win. |
| The helper's config string can embed `Profile`/`Region` | `DynamoDBStreamsEventSourceResource.cs` copies them from the Lambda's `SDKResourceAnnotation.SdkConfig` | A baked-in profile or a region diverging from the injected `AWS_DEFAULT_REGION` can bypass injected credentials. Pre-existing shared risk with SQS; document it and decide whether to mitigate. |

Recommended support shape:

| Required area | Expected change |
| --- | --- |
| Constants | Add `DynamoDBStreamsEventSourceResource = "Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource"`. |
| `UseLocalStack()` scan | Treat the DynamoDB Streams event-source helper like SQS and attach `.WithReference(localStack)`. |
| Connection-string callback | Add a branch for the DynamoDB Streams helper resource. |
| Resource configurator | Add a DynamoDB Streams-specific configurator that emits LocalStack endpoint and AWS credentials/region. |
| Endpoint env vars | Emit `AWS_ENDPOINT_URL`, `AWS_ENDPOINT_URL_DYNAMODB`, and `AWS_ENDPOINT_URL_DYNAMODB_STREAMS`. The service-specific vars are required, not a preference (see constraints above). |
| Tests | Add reflection guard tests and unit coverage for detection/configuration. Integration coverage should be considered after the unit path is stable. |

Note on shape: there is no pluggable configurator pattern to extend. `LocalStackResourceConfigurator` is a static class, and detection lives in two hardcoded dispatch chains that must be edited in lockstep (`LocalStackResourceBuilderExtensions.cs:103-107` and `LocalStackConnectionStringAvailableCallback.cs:64-68`) plus `Constants.cs` and the configurator — a copy-the-SQS-branch change across four files. The new DynamoDB Streams branch must sit before the relationship-annotation branch in both chains, as SQS does.

## CDK Asset Upload Check

Upstream `13.3.1` has `src/Aspire.Hosting.AWS/Provisioning/CDKAssetUploader.cs`. It creates `AmazonS3Client` and `AmazonSecurityTokenServiceClient` through the AWS SDK config path and uses S3 operations to upload file assets.

This repo's `LocalStackCdkAssetUploadEndpointCustomizer` remains relevant:

| Repo file/test | Finding |
| --- | --- |
| `src/Aspire.Hosting.LocalStack/Internal/LocalStackCdkAssetUploadEndpointCustomizer.cs` | Hooks S3 and STS SDK runtime pipelines after endpoint resolution; S3 also forces path-style addressing before endpoint resolution. |
| `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackCdkAssetUploadEndpointCustomizerTests.cs` | Tests handler ordering around `AmazonS3EndpointResolver` and `AmazonSecurityTokenServiceEndpointResolver`. |
| `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs` | CDK stack `AWSSDKConfig` is region-pinned and profileless for LocalStack. |

No immediate CDK asset upload change is recommended from this pass. The existing tests are useful because they pin the fragile AWS SDK pipeline hook positions.

## LocalStack Platform Constraints

Added during re-validation — the original pass recommended the DynamoDB Streams adaptation without evidencing LocalStack-side support. Verified 2026-07-02:

| Fact | Source | Impact |
| --- | --- | --- |
| DynamoDB Streams is available on all LocalStack plans, including the free Hobby plan | `https://docs.localstack.cloud/aws/services/dynamodbstreams/` | The WS2 feature is viable on LocalStack. |
| Stream persistence is not supported: stream configuration is lost across container restarts even where table data persists | Same page | Matters for `ContainerLifetime.Persistent` scenarios and restart-sensitive tests. |
| Streams are Kinesis-backed with a history of shard-iterator/sequence-number flakiness (upstream LocalStack issues #11599, #2781, #2959); streams are not supported on replicated tables | LocalStack docs + GitHub issues | Supports the unit-tests-first, integration-after-confidence sequencing. |
| Since 2026-03-23 LocalStack ships a single unified image, and new releases require an authenticated auth token — including in CI. A free plan exists; older pinned tags (like this repo's default `4.12.0`) remain token-free | `https://blog.localstack.cloud/localstack-single-image-next-steps/` | Gates the WS2 integration-test strategy: post-transition images need token plumbing in CI, while staying on the pinned pre-transition tag means testing streams behavior against a frozen image. Couples WS2 to WS7 (unified-image migration, issue #25), whose urgency is higher than previously noted. |

## Repo Impact Summary

| Repo area | Finding |
| --- | --- |
| `src/Aspire.Hosting.LocalStack/Internal/Constants.cs` | Missing DynamoDB Streams event-source type constant. |
| `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs` | `UseLocalStack()` only detects SQS event-source helper executables. |
| `src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs` | Four dispatch branches (CDK stack, CloudFormation template, SQS helper by type name, project by relationship annotation) plus CDK credentials/asset-upload follow-up; no DynamoDB Streams branch. |
| `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs` | Project resources receive `LocalStack__*`; SQS helpers receive global `AWS_ENDPOINT_URL`; no DynamoDB Streams helper support. |
| `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/ConstantsTests.cs` | Good guard pattern exists and should be extended for DynamoDB Streams. |
| `tests/Aspire.Hosting.LocalStack.Unit.Tests/Internal/LocalStackConnectionStringAvailableCallbackTests.cs` | Better than a stub: one test drives the real callback end-to-end through the CDK-stack branch, and `LocalStackResourceConfiguratorTests.cs` covers env injection for all four configure methods. Still untested through the callback: SQS and project branch dispatch, and the `LOCALSTACK_HOST` assignment (line 40). WS5 scope. |
| `playground/lambda/LocalStack.Lambda.AppHost/Program.cs` | Uses SQS event source only; no DynamoDB Streams sample. |

## Relationship To WS3

WS2 and WS3 are linked. WS2 finds a concrete feature gap, but WS3 remains the broader architectural move.

AWS's official service-specific endpoint documentation confirms this precedence:

1. Explicit endpoint in code/client config.
2. Service-specific environment variable such as `AWS_ENDPOINT_URL_DYNAMODB`.
3. Global `AWS_ENDPOINT_URL`.
4. Shared config service endpoint.
5. Shared config profile endpoint.
6. Default service endpoint.

That supports WS3's direction: this package should keep `LocalStack__*` for `LocalStack.Client.Extensions` compatibility and add AWS-official `AWS_ENDPOINT_URL_<SERVICE>` / `AWS_ENDPOINT_URL` emission for bare AWS SDK usage.

For DynamoDB Streams specifically, using `AWS_ENDPOINT_URL_DYNAMODB` and `AWS_ENDPOINT_URL_DYNAMODB_STREAMS` aligns with upstream AWS integration behavior and makes future WS3 endpoint work more consistent.

## Recommended Follow-Up Order

1. Design and implement WS2's small feature adaptation: DynamoDB Streams event-source support for LocalStack.
2. Add/extend guard tests for reflected AWS internal types and event-source configuration paths.
3. Continue WS3 design for AppHost decoupling from `LocalStack.Client` and native endpoint emission.
4. Fold callback behavior coverage into WS5 because it is currently under-tested and overlaps both WS2 and WS3.

## Open Questions

| Question | Suggested owner/workstream |
| --- | --- |
| Endpoint env vars for DynamoDB Streams: global only or service-specific too? | Closed during re-validation: service-specific `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS` are required — AWS SDK precedence and upstream's own DynamoDB-local wiring would otherwise override a global-only value. |
| Should implementation-detail helper resources be hidden with `WithHidden()` instead of only `ExcludeFromManifest()`? | WS6/UX, not required for WS2. |
| Should DynamoDB Streams get integration coverage immediately or only unit coverage first? | WS2/WS5. Recommendation: unit first; integration after confidence on LocalStack DynamoDB Streams behavior (streams persistence is unsupported across restarts, and the image/auth-token choice in LocalStack Platform Constraints gates CI). |
| Should `UseLocalStack()` continue string-matching each internal helper type or introduce a small internal abstraction over known AWS helper resources? | WS6/API quality. Keep minimal for WS2. |

## Bottom Line

Aspire core `13.4.6` is compatible with the package's current hosting patterns. `Aspire.Hosting.AWS` `13.3.1` keeps existing CloudFormation/SQS internal type names stable (both files byte-identical to `9.3.0`), so current functionality is not broken.

The highest-value WS2 adaptation is adding LocalStack support for the new `DynamoDBStreamsEventSourceResource` helper, with four evidence-backed design constraints: route through the `WithReference`/`WaitFor` path, emit the service-specific endpoint vars (required, not optional), pin the env-callback ordering in tests, and account for LocalStack's streams-persistence limitation and unified-image/auth-token transition in the test strategy. AgentCore and AWS publish/deploy remain explicitly deferred because they do not map cleanly to LocalStack local-development behavior.
