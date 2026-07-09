# Aspire Annotation-Collection Race — CI Startup Flake Investigation

Date: 2026-07-09

## Why This Exists

The CI run for commit `16a3223` ([run 29018595061](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/runs/29018595061), attempt 3, Linux job) failed 11 integration tests at once with `System.InvalidOperationException: Collection was modified; enumeration operation may not execute.` A plain re-run of the same commit (attempt 4) passed, and the full integration suite passed locally on Windows. This document records the root-cause evidence so the failure is recognized on sight, not re-investigated, and defines when to revisit.

Key context: integration tests run only on the Linux CI job (`.github/workflows/ci-cd.yml`, `if: runner.os == 'Linux'`), so this flake only ever surfaces there.

## Symptom

- All failing tests reported `0ms` and the identical exception: they never ran. The shared `LocalStackLambdaFixture.InitializeAsync` (`tests/Aspire.Hosting.LocalStack.Integration.Tests/TestInfrastructure/LocalStackLambdaFixture.cs:44`) crashed while starting the Lambda playground AppHost, and TUnit propagated the fixture failure to every test bound to it.
- The trigger commit (`16a3223`) touched only `playground/provisioning/LocalStack.Provisioning.Frontend` — no code path shared with the Lambda fixture. The commit did not cause the failure; timing did.

Failing stack (verbatim from the CI log, repeated identically for every test):

```text
System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
   at System.Linq.Enumerable.OfTypeIterator`1.ToArray()
   at Aspire.Hosting.ApplicationModel.ResourceExtensions.TryGetAnnotationsOfType[T](...) ResourceExtensions.cs:56
   at Aspire.Hosting.ApplicationModel.ResourceExtensions.TryGetEndpoints(...)            ResourceExtensions.cs:689
   at Aspire.Hosting.Dcp.DcpModelUtilities.AreResourceEndpointsAllocated(...)            DcpModelUtilities.cs:260
   at Aspire.Hosting.Dcp.DcpModelUtilities.TryAddWorkloadAllocatedEndpoints[T](...)      DcpModelUtilities.cs:127
   at Aspire.Hosting.Dcp.DcpExecutor.RunApplicationAsync(...)                            DcpExecutor.cs:204/216/244
```

## Root Cause — Unsynchronized Reader + Start-Time Writer

Two parties touch the same `IResource.Annotations` list concurrently during AppHost startup.

### Reader (upstream, Aspire 13.4.6)

`DcpExecutor.RunApplicationAsync` prepares resources in parallel per-resource tasks; each task enumerates annotation collections via `TryGetAnnotationsOfType[T]` → `OfType<T>().ToArray()`. In 13.4.6, `ResourceAnnotationCollection` is a plain `Collection<IResourceAnnotation>` with no synchronization (verified in the pinned checkout: `external/aspire/v13.4.6/src/Aspire.Hosting/ApplicationModel/ResourceAnnotationCollection.cs:11`).

### Writer (this package — strongest candidate, not oracle-proven)

`LocalStackConnectionStringAvailableCallback` (`src/Aspire.Hosting.LocalStack/Internal/LocalStackConnectionStringAvailableCallback.cs`) runs on `ConnectionStringAvailableEvent` — i.e. mid-startup, as soon as the LocalStack container's endpoint is allocated — and mutates annotation collections while DCP's parallel tasks are still enumerating them:

- `WithEnvironment("LOCALSTACK_HOST", ...)` on the LocalStack resource itself (line 40).
- Via `LocalStackResourceConfigurator` (`WithEnvironment(context => ...)` at lines 67/95/114): adds `EnvironmentCallbackAnnotation`s to SQS and DynamoDB Streams event-source resources (both `ExecutableResource`s in the AWS integration) and to referencing project resources.

The Lambda playground exercises exactly this path: `WithSQSEventSource` and `WithDynamoDBStreamsEventSource` (`playground/lambda/LocalStack.Lambda.AppHost/Program.cs:56,62`).

The exception does not identify the concurrent writer, so writer identity is inference: this package is the only known model mutator at that moment besides Aspire internals. The reader-side race is proven by the stack trace regardless.

### Why Linux CI and not local Windows

Slower 2-core hosted runners widen the overlap between the LocalStack container becoming ready (event fires → writes) and DCP's endpoint-allocation sweep over the remaining resources (reads). The full suite passed locally on Windows on the same commit; a plain CI re-run passed. Classic timing-dependent race behavior.

## Upstream State (verified 2026-07-09)

The Aspire repository moved from `dotnet/aspire` to **`microsoft/aspire`** (git remotes redirect; GitHub search needs the new name).

- **Fixed at the collection level on `main`**: [PR #18259](https://github.com/microsoft/aspire/pull/18259) "Make ResourceAnnotationCollection thread-safe" (merged 2026-06-19, commit `c07d86fff`) rebuilds the collection on an `ImmutableArray<T>` backing store — lock-free snapshot reads, locked writes — while keeping `Collection<T>` as base for binary compatibility.
- **Not in any shipped release**: `v13.4.6` was tagged 2026-06-20 from a cut that predates the fix (verified by reading the file in the pinned `external/aspire/v13.4.6` checkout). As of 2026-07-09, 13.4.6 is the newest `Aspire.Hosting` on NuGet — no 13.4.7, no 13.5, no preview.
- Same bug class previously reported and closed upstream: [#17083](https://github.com/microsoft/aspire/issues/17083) (13.3.2, `ContainerCreator` path, fixed for 13.4) and [#15666](https://github.com/microsoft/aspire/issues/15666) (Aspire's own CI flaked with the same exception in a Redis test).

No new upstream issue is needed — the collection-level fix already merged and covers our call site.

## Decision (2026-07-09)

**Wait for the upstream release; do not hot-fix package code.** If the flake recurs before then, re-run the job. Rationale: the crash class disappears wholesale with the first Aspire release containing #18259, and the alternative (refactoring a core wiring path under CI pressure) carries more regression risk than the flake costs.

A deliberate refactor remains on the table as a **design-quality item, independent of the crash** (tracked in WS6): move environment-annotation registration out of `ConnectionStringAvailableEvent` to model-construction time, using deferred `WithEnvironment(context => ...)` callbacks that resolve LocalStack endpoint values lazily; keep only non-annotation side effects (CloudFormation client setup, CDK credential override, asset-upload endpoint customizer) in the event callback. Event-time model mutation is fragile beyond the race — annotations added after a target's environment has been computed are silently ignored. **Caution:** this refactor changes callback ordering. Today "this package's callbacks always run last" (last-writer-wins over upstream's build-time callbacks) is a load-bearing property noted in WS3's endpoint-precedence contract, and it exists *because* registration happens at connection-string-available time. Moving registration to build time must be designed together with WS3's defaults-not-overrides contract, not done as a mechanical move.

## Revisit Triggers

1. **A new `Aspire.Hosting` version appears on NuGet** → bump `Directory.Packages.props`, refresh the `external/aspire` checkout to the new tag, and verify `ResourceAnnotationCollection.cs` at that tag contains the `ThreadSafeAnnotationList` backing store. Then mark this investigation resolved in the roadmap.
2. **The flake recurs frequently before an upstream release ships** → escalate the WS6 construction-time-registration refactor from backlog to planned, designed jointly with WS3's precedence contract.
3. **WS3/WS6 eventing/callback redesign work starts** → fold the construction-time registration decision in; do not leave it as an orphaned bullet.
