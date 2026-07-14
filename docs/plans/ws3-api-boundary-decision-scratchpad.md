# WS3A API Boundary Decision Scratchpad

Date: 2026-07-10

Status: Decision record supporting `2026-07-14-ws3a-apphost-api-boundary.md`; the implementation plan is authoritative where wording differs.

## Purpose

Preserve the decisions and evidence from the WS3A brainstorming session before the native-endpoint spike and formal specification. Future work should update this file when a working decision changes instead of reconstructing the discussion from memory.

## Problem Statement

The WS3A problem is not that `LocalStack.Client` exists in the package dependency graph. The problem is that dependency-owned configuration models currently leak into the hosting package's public contract and make AppHost users learn concepts that the hosting integration should own.

The design must keep these concerns separate:

- Internal runtime dependency versus public API ownership.
- AppHost orchestration configuration versus workload client configuration.
- Hosting settings versus LocalStack container settings.
- Public-surface cleanup versus issue #12 native AWS SDK endpoint support.

## Working Decisions

### Keep LocalStack.Client internally

`LocalStack.Client` remains an implementation dependency.

Reasons:

- `SessionStandalone` and `Session` create AWS clients with LocalStack credentials, region, endpoint, and proxy behavior.
- Proxy mode avoids the custom-`ServiceURL` signing-region defect described in `docs/plans/aws-sdk-signing-region-investigation.md`.
- `AwsServiceEndpointMetadata` owns the canonical metadata for 124 AWS services. Reproducing that table here would create avoidable maintenance debt.
- The workload `LocalStack__*` configuration schema is the contract consumed by `LocalStack.Client.Extensions`.

This decision does not permit `LocalStack.Client` configuration types to define the new hosting API.

### Own the hosting configuration model

The recommended API uses this flat package-owned model:

```csharp
public sealed class LocalStackHostingOptions
{
    public bool Enabled { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string AccessKeyId { get; set; } = "accessKey";
    public string SecretAccessKey { get; set; } = "secretKey";
    public string SessionToken { get; set; } = "token";
}
```

The defaults preserve `LocalStack.Client` 2.0.0 behavior. The flat shape follows standard AWS credential terminology and avoids a nested credentials type for three values that are always consumed together.

The owned model should contain only settings that are meaningful to the AppHost integration:

- `Enabled` state.
- AWS `Region`.
- LocalStack AWS `AccessKeyId`.
- LocalStack AWS `SecretAccessKey`.
- LocalStack AWS `SessionToken`.

It should not mirror the full `LocalStack.Client` options tree.

- Host and port come from Aspire's allocated endpoint.
- SSL must be modeled as a real resource endpoint capability before becoming a first-class hosting setting.
- Legacy per-service ports do not belong in the modern single-edge-port container model.

Legacy settings continue to work during the compatibility period even when they have no counterpart in the new recommended model.

### Use owned options and focused fluent APIs

The intended C# experience combines:

- A mutable package-owned options model for configuration binding, validation, test overrides, and future ATS/polyglot DTO support.
- A required C# configuration callback for explicit overrides after configuration binding.
- Focused fluent methods on the owned options model for discoverable configuration such as enablement, region, and credentials.
- The existing container callback for container-only concerns such as image, lifetime, logging, port, volumes, environment, and Docker socket behavior.

Enablement must be resolved before resource creation, so all hosting options and their fluent methods run inside the pre-add configuration callback. The existing nullable return behavior is preserved.

The focused fluent methods are:

```csharp
WithEnabled(bool enabled)
WithRegion(string region)
WithCredentials(string accessKeyId, string secretAccessKey, string sessionToken)
```

They mutate and return the callback's `LocalStackHostingOptions` instance. They do not introduce another state object or post-add ordering requirement.

The 13.x package-owned overload family is:

```csharp
AddLocalStack();

AddLocalStack(string name);

AddLocalStack(
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackContainerOptions>? configureContainer);

AddLocalStack(
    string name,
    IAWSSDKConfig? awsConfig,
    Action<LocalStackHostingOptions> configureOptions,
    Action<LocalStackContainerOptions>? configureContainer = null);
```

The argument order is resource identity first, AWS configuration second, then the hosting and container callbacks together. The two longer overloads delegate to one implementation. They remain separate in 13.x because making `configureOptions` optional causes CS0121 against the released all-optional legacy overload when the callback is omitted.

### Translate to Client models only inside the implementation

The package may construct `SessionOptions` and `ConfigOptions` internally when calling `SessionStandalone`.

This is a small, one-way translation rather than a second public abstraction layer:

1. Resolve package-owned hosting state.
2. Resolve the Aspire endpoint.
3. Construct the `LocalStack.Client` runtime values required by `SessionStandalone`.
4. Keep those Client types behind the package boundary.

Workload environment emission should use the package-owned state directly rather than round-tripping through Client options.

### Preserve existing disabled and nullable behavior

WS3A does not redesign LocalStack enablement semantics.

Current behavior is intentional:

1. `AddLocalStack` resolves configuration.
2. If `UseLocalStack` is false, it adds no resource and returns `null`.
3. `UseLocalStack(null)` and `WithReference(..., null)` are no-ops.
4. The official AWS resources therefore retain their normal real-AWS behavior without conditional AppHost code.

The owned model replaces only the source of the enablement value:

```text
ILocalStackOptions.UseLocalStack -> LocalStackHostingOptions.Enabled
```

The runtime branch remains equivalent. A non-null LocalStack resource is necessarily enabled, so internal checks of `Resource.Options.UseLocalStack` can become null checks without changing behavior. No conditional-resource abstraction, registration handle, or always-present disabled resource belongs in WS3A.

### Keep the explicit `UseLocalStack` resource selector

The canonical API remains:

```csharp
var localStack = builder.AddLocalStack();
builder.UseLocalStack(localStack);
```

The nullable `localStack` argument is not a second configuration source. It selects the concrete LocalStack resource whose package-owned state, endpoint, wait relationships, connection references, and annotations are used for wiring. The resource remains the single source of truth.

`UseLocalStack(IResourceBuilder<ILocalStackResource>?)` remains supported and is not obsolete. WS3A does not add an argumentless `UseLocalStack()` overload. Argumentless discovery would introduce call-order ambiguity when no resource exists and would require a new policy for multiple LocalStack resources.

This is consistent with repository history: the first `UseLocalStack` implementation already scanned `builder.Resources` for AWS resources while deliberately accepting the LocalStack resource as an explicit selector. Detailed evidence is recorded in `docs/plans/ws3-use-localstack-resource-selector-research.md`.

### Use a hosting-specific canonical configuration section

The new AppHost configuration section is:

```text
Aspire:Hosting:LocalStack
```

This follows the nearest pinned hosting precedent, `Aspire:Hosting:BrowserLogs`, and aligns with the `Aspire.Hosting.LocalStack` namespace.

The standard .NET environment-variable form is:

```text
Aspire__Hosting__LocalStack__Enabled
```

The legacy `LocalStack:*` section remains a compatibility fallback. When both sections define the same conceptual setting, the new canonical section wins.

Working precedence, lowest to highest:

1. Defaults.
2. Legacy `LocalStack:*` configuration.
3. Canonical `Aspire:Hosting:LocalStack:*` configuration.
4. Explicit package-owned options.
5. Fluent overrides.

Special compatibility rules:

- An explicitly supplied legacy `ILocalStackOptions` instance must retain its current precedence rather than being silently overwritten by configuration binding.
- `IAWSSDKConfig.Region` continues to override the LocalStack region as it does today.
- Exact precedence validation and duplicate-setting diagnostics belong in the formal specification.

The AppHost setting and workload setting are different contracts:

- `Aspire:Hosting:LocalStack:Enabled` decides AppHost orchestration behavior.
- Emitted `LocalStack__UseLocalStack` continues to control `LocalStack.Client.Extensions` inside workloads.

### Validate the resolved enabled configuration

Validation runs after configuration providers, the explicit hosting callback, and the `IAWSSDKConfig.Region` compatibility override have produced the final package-owned state.

When `Enabled` is false, no LocalStack resource is created and the unused region and credentials are not validated.

When `Enabled` is true:

- `Region`, `AccessKeyId`, `SecretAccessKey`, and `SessionToken` must not be null, empty, or whitespace.
- Region names are not restricted to the AWS SDK's current known-region list; LocalStack and future AWS SDK versions may use identifiers unknown to the pinned SDK.
- Validation failures are aggregated into a `DistributedApplicationException` that names invalid properties without logging credential values.

### Keep AwsService as a deliberate exception

`LocalStack.Client.Enums.AwsService` may remain in `EagerLoadedServices` for this workstream.

Unlike the Client options models, this enum represents shared LocalStack service vocabulary backed by the canonical 124-service metadata table. Replacing it now would require duplicated enum metadata or a less discoverable string API.

This is a conscious public dependency exception, not an accidental omission. A future ATS/polyglot export adapter may expose service names without changing the C# API.

### Preserve compatibility through additive deprecation

The package published the current Client-owned surface in `13.4.0`. WS3A must not remove those members from the 13.x metadata surface.

The intended migration policy is:

- Add and document the package-owned path.
- Keep existing behavior working in 13.x.
- Mark legacy members obsolete and add exact forwarding overloads for the approved package-owned call shapes. Existing named `awsConfig:` or `configureContainer:` calls that omit `name` still bind the released overload and receive CS0618.
- Remove legacy Client-owned members only in a future major release.

The overload compatibility strategy is now resolved:

- Keep the released all-optional `AddLocalStack` overload in metadata for source and binary compatibility, but mark it obsolete.
- Add exact package-owned forwarding overloads for parameterless, name-only, AWS/container, and explicit hosting configuration call shapes.
- Route every new overload into one package-owned implementation path.
- Do not add a custom Roslyn analyzer. Exact overloads protect parameterless, name-only, and explicit name/AWS/container calls; calls that still bind the released overload receive the standard obsolete warning.
- Mark the remaining Client-owned entry points obsolete, including `AddLocalStackOptions`, the public resource options property, constructors, and Client-options fluent extensions.

`OverloadResolutionPriorityAttribute` is not a viable compatibility mechanism because it requires a C# 13-or-later consumer compiler. Pre-C# 13 consumers can still see ambiguous calls. Detailed SDK 8/9/10 source and binary results are recorded in `docs/plans/ws3-addlocalstack-overload-compatibility-spike.md`.

Deprecations use the standard non-error `ObsoleteAttribute` and CS0618. No custom diagnostic IDs are introduced.

Migration messages are grouped by replacement:

- Legacy `AddLocalStack(... ILocalStackOptions ...)`: use a package-owned `AddLocalStack` overload and `LocalStackHostingOptions`.
- `AddLocalStackOptions()`: configure `LocalStackHostingOptions` through the `AddLocalStack` callback.
- `ILocalStackResource.Options`: hosting state is no longer public; configure it through `AddLocalStack`.
- `LocalStackResource(string, ILocalStackOptions)`: create the resource through `IDistributedApplicationBuilder.AddLocalStack`.
- `WithUseLocalStack`: use `LocalStackHostingOptions.WithEnabled`.
- `WithRegion`: use `LocalStackHostingOptions.WithRegion`.
- `WithSessionOptions`: use `WithRegion` and `WithCredentials` on `LocalStackHostingOptions`.
- `WithEdgePort`, `WithLocalStackHost`, `WithUseSsl`, and `WithConfigOptions`: configure the Aspire endpoint or `LocalStackContainerOptions`; these Client connection settings have no package-owned equivalent.

Every message states that removal is deferred to the next major version. The messages name replacements but do not embed the current package version, avoiding stale text across 13.x patch and minor releases.

## Current Public Leakage Inventory

Verified in `LocalStack.Aspire.Hosting` 13.4.0:

- `AddLocalStack(... ILocalStackOptions? localStackOptions ...)`.
- `AddLocalStackOptions()`.
- `ILocalStackResource.Options`.
- `LocalStackResource(string, ILocalStackOptions)`.
- Seven methods in `LocalStackConfigurationExtensions` that return or accept Client-owned options.
- `LocalStackContainerOptions.EagerLoadedServices` through `AwsService`, retained as the deliberate exception above.

GitHub code search found no organic external use of `AddLocalStackOptions()` or `LocalStackConfigurationExtensions`; matches were this repository and forks. Real examples use the enablement configuration key, `IAWSSDKConfig`, and the container callback. This lowers migration risk but does not remove the compatibility obligation.

## Native AWS SDK Endpoint Coexistence

### Working decision: no package-managed native endpoint emission

Issue #12 asks for workloads that use the bare AWS SDK to receive standard endpoint configuration without taking a dependency on `LocalStack.Client.Extensions` or adding LocalStack-specific application branches. The reporter confirmed that the existing `localStack.Resource.ConnectionStringExpression` can be assigned through `WithEnvironment`, so WS3A does not add `IResourceWithEndpoints` or another native-endpoint feature.

Relevant AWS SDK variables include:

```text
AWS_ENDPOINT_URL
AWS_ENDPOINT_URL_SQS
AWS_ENDPOINT_URL_DYNAMODB
```

WS3A responsibilities are limited to:

- Preserve `LocalStack__*` emission for existing `LocalStack.Client.Extensions` consumers.
- Do not add, remove, or overwrite `AWS_ENDPOINT_URL*` on behalf of the consumer.
- When the package can observe that Client proxy configuration and `AWS_ENDPOINT_URL*` coexist on the same workload, emit a best-effort warning without mutating either value.
- Keep the limitation aligned in `README.md` Known Limitations, `docs/agents/KNOWN_ISSUES.md`, and the draft `13.4.1` changelog.

Official AWS SDKs and Tools behavior:

- `AWS_ENDPOINT_URL` sets a global custom endpoint.
- `AWS_ENDPOINT_URL_<SERVICE>` sets a service-specific endpoint and takes precedence over the global value.
- `AWS_REGION` is the canonical SDK region environment variable.
- `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` supply static credentials; `AWS_SESSION_TOKEN` supplies the session token when temporary/session credentials are used.

Primary references:

- [AWS service-specific endpoints](https://docs.aws.amazon.com/sdkref/latest/guide/feature-ss-endpoints.html)
- [AWS Region](https://docs.aws.amazon.com/sdkref/latest/guide/feature-region.html)
- [AWS access keys](https://docs.aws.amazon.com/sdkref/latest/guide/feature-static-credentials.html)

The three known-issue locations must state that custom-endpoint signing behavior is AWS SDK version-sensitive. On affected AWSSDK.Core versions, a configured non-default region can be signed as `us-east-1`; LocalStack then resolves a different regional namespace. The package does not claim to correct or abstract that upstream behavior.

Automatic dual emission is rejected. A native endpoint feature and new Client-mode abstraction have no planned implementation in any workstream. Warning detection uses an ordered environment callback appended during `BeforeStartEvent`. A warning should describe a potential conflict only when this package has enabled the Client proxy path for that workload; merely running the LocalStack container is not sufficient.

### Working decision: best-effort runtime warning

Pinned Aspire 13.4.6 source provides a viable warning mechanism without changing environment values:

1. Subscribe to `BeforeStartEvent`, which exposes the completed `DistributedApplicationModel` after normal builder composition.
2. Find environment resources configured with `LocalStackEnabledAnnotation`.
3. Capture `ResourceLoggerService.GetLogger(resource)` for each candidate.
4. Append an ordered `EnvironmentCallbackAnnotation` to each candidate resource.
5. Inspect the shared environment dictionary when Aspire evaluates callbacks.
6. Use the captured logger to emit a resource-scoped warning.

The logger must not come from `EnvironmentCallbackContext.Logger`. Aspire container dependency discovery can evaluate and cache callbacks with `NullLogger` before final environment gathering, preventing a later callback execution with the normal context logger.

The warning condition is the coexistence of:

```text
LocalStack__UseLocalStack=true
AWS_ENDPOINT_URL or AWS_ENDPOINT_URL_*
```

The detector must:

- Run only when the package enabled the Client proxy path for that workload.
- Match environment keys case-insensitively.
- Treat global and service-specific native endpoint keys as conflicts.
- Avoid warnings for Lambda helper resources that intentionally use native endpoints without `LocalStack__UseLocalStack=true`.
- Avoid warnings when `LocalStack__UseLocalStack=false` selects the standard AWS client factory.
- Log only; never add, remove, or overwrite environment values.
- Be covered for callbacks registered before the package appends its warning callback.

The guarantee is ordered rather than absolute. The callback sees annotations registered before this package appends it during `BeforeStartEvent`. Another later `BeforeStartEvent` subscriber, an obsolete lifecycle hook, or a pipeline step can append an annotation afterward, which the warning callback will not observe. Pinned Aspire 13.4.6 exposes no hook that guarantees the final annotation position immediately before environment evaluation, so documentation must call this detection best-effort.

### Compatibility question resolved by spike

LocalStack.Client issue #27 reports that setting `AWS_ENDPOINT_URL` can break LocalStack.NET endpoint resolution and asks the Client to account for native endpoint configuration. The issue remains open; the completed spike below is summarized in a durable investigation comment linked under Evidence And References.

Source inspection shows a plausible conflict:

1. `LocalStack.Client.Session` configures `RegionEndpoint` plus `ProxyHost` and `ProxyPort`.
2. AWS SDK `ClientConfig.ServiceURL` can later auto-resolve `AWS_ENDPOINT_URL*`.
3. A Client-created AWS client may therefore have both LocalStack proxy mode and a custom service URL.

Existing Client tests do not cover that combination. The previous signing-region investigation proves that proxy mode alone is safe; it does not prove that proxy mode plus endpoint environment variables is safe.

The statement that dual emission leaves existing Client consumers unchanged must not appear in the specification unless the spike proves it.

### Spike result (completed and independently verified 2026-07-10)

A throwaway probe was created under `/tmp/opencode/ws3-dual-emission-spike`. It starts two independent LocalStack containers with random host ports and seeds distinct SQS marker queues in each instance and region. The client path uses the real `AddLocalStack(configuration)` plus `AddAwsService<IAmazonSQS>()` DI flow.

The parent reran the complete matrix with:

```bash
cd /tmp/opencode/ws3-dual-emission-spike
./run-matrix.sh
```

The rerun completed for all requested variants and left no probe containers running.

Verified matrix:

- No native endpoint environment variable.
- Global `AWS_ENDPOINT_URL`.
- Service-specific `AWS_ENDPOINT_URL_<SERVICE>`.
- `LocalStack.Client.Extensions` client creation.
- Bare AWS SDK client creation.
- A non-default region such as `eu-central-1` in addition to `us-east-1`.
- The LocalStack.Client 2.0.0 floor Core `4.0.0.15`, using the nearest compatible SQS `4.0.0.14` because SQS `4.0.0.15` requires Core `4.0.0.16`.
- The repository pins: Core `4.0.9.6` and SQS `4.0.3.7`.
- Current affected line: Core and SQS `4.0.100.2`.

Observed configuration and runtime behavior:

- `AWS_ENDPOINT_URL` and `AWS_ENDPOINT_URL_SQS` populate `ClientConfig.ServiceURL` even for clients created through LocalStack.Client.Extensions proxy mode.
- When Client configuration points to A and native endpoint configuration points to B, the client has `ServiceURL=B` and `ProxyHost/ProxyPort=A`, but actual SQS traffic reaches A.
- Reversing the endpoints produces the inverse result: the Client proxy endpoint still wins.
- Service-specific `AWS_ENDPOINT_URL_SQS` wins over global `AWS_ENDPOINT_URL` when resolving `ServiceURL`.
- No tested SQS collision produced a timeout, exception, proxy loop, or request to both containers. This does not make the mixed configuration safe.
- With Core `4.0.9.6`, the configured non-default region survives and the expected region-scoped marker is returned.
- With the `4.0.0.15` floor line, native endpoint configuration clears or loses the effective region and an `eu-central-1` request reaches the `us-east-1` namespace.
- With Core `4.0.100.2`, `RegionEndpoint` still displays `eu-central-1`, but the signed request reaches LocalStack's `us-east-1` namespace, matching the known custom-endpoint signing regression.
- LocalStack.Client proxy mode without native endpoint variables reaches the configured proxy and region on all three tested lines.
- Bare AWS SDK clients reach the native endpoint, but their effective signing region still depends on the Core version.

Representative collision result:

```text
LocalStack.Client endpoint: A
AWS_ENDPOINT_URL:           B
ClientConfig.ServiceURL:    B
ClientConfig.Proxy:         A
Actual SQS instance:        A
```

Conclusion from evidence: automatic dual emission is unsafe for existing LocalStack.Client proxy-mode consumers. It creates a mixed client state and silently changes the effective region on affected Core versions. WS3A will not emit native endpoint variables automatically.

### Native-mode mitigation result (verified 2026-07-10)

A follow-up matrix tested workloads that still install and call `LocalStack.Client.Extensions`, but receive:

```text
LocalStack__UseLocalStack=false
AWS_ENDPOINT_URL=<LocalStack endpoint>
AWS_REGION=<configured region>
AWS_ACCESS_KEY_ID=<local credentials>
AWS_SECRET_ACCESS_KEY=<local credentials>
AWS_SESSION_TOKEN=<local credentials>
```

Both global `AWS_ENDPOINT_URL` and service-specific `AWS_ENDPOINT_URL_SQS` were tested. LocalStack host and port values deliberately pointed at the other container to detect accidental proxy use.

Observed behavior across all three Core lines:

- LocalStack.Client.Extensions selected its standard AWS client factory path.
- `ProxyHost` was null and `ProxyPort` was zero.
- `ServiceURL` contained the native endpoint.
- Actual SQS traffic reached the native endpoint container.
- Setting `LocalStack__UseLocalStack=true` with otherwise identical variables restored the mixed proxy/native collision.

This proves a viable routing separation if a future explicit helper or per-reference mode is justified by user demand:

- Legacy Client mode emits `LocalStack__UseLocalStack=true` plus the Client configuration and does not emit native endpoint variables.
- Native AWS SDK mode emits `LocalStack__UseLocalStack=false` plus standard AWS endpoint, region, and credential variables.

The mitigation fixes endpoint and proxy selection only. It does not fix the AWS SDK custom-endpoint signing regression:

- Core `4.0.9.6` preserves `eu-central-1`.
- The `4.0.0.15` floor and Core `4.0.100.2` reach the native endpoint but sign into LocalStack's `us-east-1` namespace for an `eu-central-1` request.

The spike shows what a hypothetical future native mode would require, but no current workstream owns or plans that abstraction. Standard `WithEnvironment` already provides the explicit native path requested by issue #12.

The probe inspected:

- `ServiceURL`.
- `RegionEndpoint`.
- `AuthenticationRegion`.
- `ProxyHost` and `ProxyPort`.
- Actual request destination.
- Signed region.
- Whether a proxy loop or endpoint override occurs.

The original acceptance conditions for automatic dual emission remain:

- Existing Client-created workloads still reach the intended LocalStack endpoint.
- Existing Client-created workloads retain the intended signing region.
- Bare SDK workloads reach LocalStack through the native endpoint contract.
- User-provided endpoint variables win over package defaults.
- The package can explain behavior across affected AWS SDK versions without claiming a guarantee it cannot provide.

The conditions failed on supported and current AWS SDK lines. The automatic dual-emission option is rejected for WS3A.

Probe limitations and follow-up candidates:

- Only SQS was tested.
- Signed region was inferred from LocalStack's region-scoped queue markers rather than capturing the raw `Authorization` header.
- Successful runs used `localstack/localstack:3.8.1`.
- A future mitigation spike should test `useServiceUrl: true`, at least one path-style-sensitive service such as S3, and the exact environment shape emitted by an Aspire workload.
- Raw artifacts and one-command reproduction currently live in `/tmp/opencode/ws3-dual-emission-spike`; the durable conclusions are recorded here so the temp directory is not a dependency.

## Explicit Non-Decisions

The following are not decided by this scratchpad:

- How ATS/polyglot exports represent callbacks and `AwsService`.
- Target-aware helper attachment and app-like/container auto-wiring; those require WS3B research.

## Evidence And References

- `src/Aspire.Hosting.LocalStack/LocalStackResourceBuilderExtensions.cs` - current add/config binding and Client options parameter.
- `src/Aspire.Hosting.LocalStack/LocalStackResource.cs` - current public Client options property and constructor.
- `src/Aspire.Hosting.LocalStack/Configuration/LocalStackConfigurationExtensions.cs` - current dependency-owned fluent surface.
- `src/Aspire.Hosting.LocalStack/Container/LocalStackContainerOptions.cs` - package-owned container settings and `AwsService` use.
- `src/Aspire.Hosting.LocalStack/Internal/LocalStackResourceConfigurator.cs` - Client runtime construction and workload environment emission.
- `docs/plans/aws-sdk-signing-region-investigation.md` - verified AWS SDK custom-endpoint signing behavior.
- `docs/plans/ws3-addlocalstack-overload-compatibility-spike.md` - SDK 8/9/10 overload-resolution and binary-compatibility evidence.
- `docs/plans/ws3-use-localstack-resource-selector-research.md` - explicit `UseLocalStack` selector rationale and history.
- `docs/plans/ws3-terminal-environment-warning-spike.md` - callback order, logger caching, non-mutation, and late-callback limitation evidence.
- `docs/plans/ws10-aspire-ecosystem-integration-research.md` - ATS/polyglot and connection-property dependencies on the post-WS3A API.
- [dotnet-aspire-for-localstack issue #12](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/12) - native AWS SDK endpoint request.
- [localstack-dotnet-client issue #27](https://github.com/localstack-dotnet/localstack-dotnet-client/issues/27) - native endpoint environment conflict with LocalStack.NET.
- [LocalStack.Client #27 runtime investigation update](https://github.com/localstack-dotnet/localstack-dotnet-client/issues/27#issuecomment-4937111791) - durable two-container matrix results and Client-side fix candidates.
- Pinned Aspire source: `external/aspire/v13.4.6`.
- Pinned AWS integration source: `external/aws-integrations/release_2026-06-19` (`13.3.1`).
- Pinned LocalStack.Client source: `external/localstack-dotnet-client/v2.0.0`.

## Next Sequence

1. Execute `docs/plans/2026-07-14-ws3a-apphost-api-boundary.md` task by task after approval and workspace preflight.
2. Keep WS3B research separate from WS3A implementation.
3. Update WS3A roadmap status as the implementation starts and completes.
