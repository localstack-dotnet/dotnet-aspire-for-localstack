# WS3A Terminal Environment Warning Spike

**Date**: July 14, 2026

## Question

Can Aspire.Hosting 13.4.6 detect coexistence of LocalStack.Client proxy enablement and native `AWS_ENDPOINT_URL*` configuration without changing either environment value?

## Pinned Source

- `Aspire.Hosting` `13.4.6`
- Aspire tag `v13.4.6`
- Commit `87fe259e4fc244c599019a7b1304c85a1488f248`

## Source Findings

`BeforeStartEvent` is published before DCP resource creation and environment configuration evaluation (`external/aspire/v13.4.6/src/Aspire.Hosting/DistributedApplication.cs:628-640`). A handler can therefore append an `EnvironmentCallbackAnnotation` to selected resources before their environments are built.

Environment annotations are evaluated in collection order against a shared `EnvironmentCallbackContext` (`external/aspire/v13.4.6/src/Aspire.Hosting/ApplicationModel/EnvironmentVariablesConfigurationGatherer.cs:14-35`). A callback appended after existing callbacks sees their current dictionary values and can inspect them without mutation.

The callback cannot safely depend on `EnvironmentCallbackContext.Logger`. Container dependency discovery may evaluate and cache environment callbacks before final environment gathering, using a context whose logger is `NullLogger` (`external/aspire/v13.4.6/src/Aspire.Hosting/ApplicationModel/ResourceExtensions.cs:1570-1600` and `external/aspire/v13.4.6/src/Aspire.Hosting/Dcp/ContainerCreator.cs:606-617`). The later gatherer receives the cached callback result and may not execute the callback again.

The warning callback must instead capture a resource logger obtained during `BeforeStartEvent`:

```csharp
var loggerService = beforeStartEvent.Services.GetRequiredService<ResourceLoggerService>();
var logger = loggerService.GetLogger(resource);
```

`ResourceLoggerService.GetLogger(IResource)` is public in the pinned hosting package and provides the resource-scoped logger used by Aspire (`external/aspire/v13.4.6/src/Aspire.Hosting/ApplicationModel/ResourceLoggerService.cs:44-69`).

## Probe

Throwaway artifacts: `/tmp/opencode/ws3-terminal-environment-warning`

The probe used the public `ExecutionConfigurationBuilder` environment gatherer with Aspire.Hosting 13.4.6. It appended a read-only warning callback after callbacks that supplied:

```text
LocalStack__UseLocalStack=true
AWS_ENDPOINT_URL_SQS=http://localhost:4566
```

Observed output:

```text
preceding-conflict-detected=true
warning-count=1
environment-mutated=false
later-callback-visible=false
```

The probe verifies:

- The appended callback sees values contributed by preceding environment annotations.
- A logger captured independently of `EnvironmentCallbackContext.Logger` receives the warning even when the gatherer is passed `NullLogger`.
- The warning callback does not add, remove, or overwrite environment values.
- A callback appended after the warning callback is not visible to it.

## Approved Detection Shape

During `BeforeStartEvent`:

1. Select `IResourceWithEnvironment` resources carrying `LocalStackEnabledAnnotation`.
2. Avoid duplicate registration with a package-owned marker annotation.
3. Obtain and capture `ResourceLoggerService.GetLogger(resource)`.
4. Append an `EnvironmentCallbackAnnotation` that only reads the shared dictionary.
5. Warn when `LocalStack__UseLocalStack` parses as true and a key equals `AWS_ENDPOINT_URL` or starts with `AWS_ENDPOINT_URL_`, using ordinal-ignore-case key comparison.
6. Do not mutate either configuration path.

The warning text must identify the resource and explain that LocalStack.Client proxy routing and native AWS SDK endpoint routing are both enabled. It should direct the user to choose one path and link to the package limitation documentation.

## Limitation

This is an ordered, best-effort warning rather than a proof over every possible late model mutation. `BeforeStartEvent` subscribers run sequentially in subscription order, and obsolete lifecycle hooks plus pipeline steps run after `BeforeStartEvent`. Another extension can therefore append an environment callback after this package's warning annotation. The warning callback will not observe values added later.

The package should not claim complete detection. It reliably observes environment annotations present before its `BeforeStartEvent` handler appends the warning callback. No supported pinned Aspire hook provides a guaranteed final callback position immediately before environment evaluation.
