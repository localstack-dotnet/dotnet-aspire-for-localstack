# Hosting Model

Date: 2026-10-16

How this package attaches itself to an Aspire application, and why its runtime and API boundaries are shaped the way they are. Everything here describes current behavior. It records constraints whose reasoning would otherwise require pull-request comments or commit archaeology; the changelog's API inventory and the XML documentation remain separate.

## Wiring is deferred to the connection-string callback

`AddLocalStack` creates the container resource and almost nothing else. The work that needs the container's real endpoint — emitting `LocalStack__*` to referencing workloads, pointing the CloudFormation client at LocalStack, overriding CDK credentials, customizing the asset-upload endpoint — is registered as a single callback keyed on `ConnectionStringAvailableEvent` (`Internal/LocalStackConnectionStringAvailableCallback.cs:19`). The callback fires once the container's endpoint is allocated, and dispatches per resource kind from there.

The endpoint is the reason. Aspire assigns the host port late, and under the default session lifetime it is dynamic, so nothing downstream can be computed at model-construction time. Registering at event time also gives this package a property it quietly depends on: **its environment callbacks always run last**, because they are added after everything else has registered its own.

That property is the cost as well as the benefit. Annotations added after a target's environment has already been computed are silently ignored, which makes event-time model mutation fragile beyond the obvious. Moving registration to construction time — using deferred `WithEnvironment(context => ...)` callbacks that resolve the endpoint lazily, and leaving only non-annotation side effects in the event — is tracked as a design-quality item in the [roadmap](https://github.com/localstack-dotnet/.github/blob/main/roadmap/aspire-hosting.md) (WS6). It requires an explicit precedence policy first, because it gives up the run-last property.

Origin: PR #2 (`e1537db`, `82a9d33`).

## Activation uses container-only sources and a run-only metadata guard

Container activation is separate from the immutable AWS/Client hosting state.
After enablement and the container callback, the integration selects the winning
fluent/extra-env/host source. Nonblank scalar inputs become private secret
parameters without published defaults; caller-owned secret parameter objects go
through Core's stock environment/reference binding. Disabled/publish paths do not
read an ambient activation source or create automatic activation resources.
Consumer-facing precedence and runtime choices live in
[Configuration](../CONFIGURATION.md#runtime-selection-and-container-activation).

The per-resource `BeforeStartEvent` subscriber appends an environment guard after
ordinary model construction. It checks the last effective image annotation and
the accumulated binding metadata, never a preset-intent flag or a secret value.
Only a qualified current identity requires a binding. The guard does not await
parameters, stringify them, probe licensing or move the connection event/SDK hooks.

On a real start (Aspire 13.6.4), the guard's `DistributedApplicationException`
surfaces during DCP resource creation. `StartAsync` itself returns. The LocalStack
resource reaches `FailedToStart` before any container is created, and its resource
log carries the error, which names the resource and `LOCALSTACK_AUTH_TOKEN`.
`ActivationGuardStartupTests` pins this behaviour.

Core's cached dependency-discovery path can reuse earlier callback snapshots
without repopulating a newly appended callback's context dictionary. To avoid a
false missing binding, finalization gives only this container's existing
environment callbacks a fresh annotation generation, retaining the same delegates
at the same positions, then appends the guard. No private Core cache is inspected
and no secret object is mutated. Callbacks cached before finalization can therefore
execute again; callbacks should assemble deferred parameters/reference metadata
rather than wait for secret values or depend on exactly-once side effects.
Cached and uncached dependency discovery, including genuinely missing and late
blank bindings, are covered independently.

This is not a global last-subscriber guarantee. Environment/image edits by later
lifecycle hooks may fall outside the guard's observed prefix; normal before/after
Add model-construction changes are supported. Actual provider failure, container
activation, persistent reuse and credential rotation remain runtime concerns.

## The CDK asset uploader is redirected through AWS SDK hooks, not process environment

With `Aspire.Hosting.AWS` 13.x, CDK file-asset upload runs before CloudFormation and constructs its own STS and S3 clients, separate from the CloudFormation client this package already routes. Those clients resolved real AWS credentials — an expired SSO profile, typically — and broke provisioning.

The fix sets `AWSConfigs.AWSCredentialsGenerators` so those clients receive LocalStack session credentials, and pairs it with a runtime-pipeline customizer for their endpoints (`Internal/LocalStackCdkCredentialsOverride.cs:16`, `Internal/LocalStackCdkAssetUploadEndpointCustomizer.cs`). The rejected alternative, written down in [issue #29](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/29)'s own proposed direction, was to set `AWS_*` process environment variables for the duration of the upload. That would have worked and was discarded deliberately: process-wide environment mutation from inside a hosting integration leaks into every other client in the AppHost, including ones the user configured on purpose, and it cannot be scoped to the two clients that need it.

Origin: `8fd8c99`, closing issues #29 and #30.

## Healthy means the edge responds, unless eager loading narrows it

By default the health check asks LocalStack's edge endpoint whether it is up. With `EagerLoadedServices`, the package emits `EAGER_SERVICE_LOADING=1` and `SERVICES=<list>` and requires each requested service to report running. Enabled, preloaded and health-required services need not be identical: backend provider filtering, dependency expansion and strict-loading policy affect the enabled set.

This shape arrived through an outside contribution and was reworked rather than merged as proposed. The alternatives on record in the review of [PR #8](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/8) were a second, separate health check for eager services, Aspire's `WithHttpHealthCheck`, and a hand-rolled `HttpClient` probe; the single-check-with-a-service-list design replaced them so that one resource has one health contract. Original work by Stuart Lang, credited in `42fe397`.

Do not interpret the requested list as an exact cross-image allow-list. It remains the health-required set, so adding names makes health stricter. Each resource now registers and attaches the same literal `localstack_health_{resourceName}` key; two resources cannot collide on a shared health-registration name. This fixes health registration, not multi-backend CDK isolation.

## `WithReference` deliberately overloads Aspire's own extension

`LocalStackProjectExtensions.cs:23` declares `WithReference<TDestination>(this IResourceBuilder<TDestination>, IResourceBuilder<ILocalStackResource>?)` in the `Aspire.Hosting` namespace — the same method name Aspire uses for its own reference family. It is an overload of a framework extension method, chosen so that referencing LocalStack reads exactly like referencing anything else in an AppHost, and it delegates to Aspire's own overload at `:34` after adding this package's annotation.

Two properties make it safe to live beside the framework's: the parameter type is this package's own `IResourceBuilder<ILocalStackResource>`, so no call that previously bound Aspire's overload changes meaning; and a null or disabled argument makes the call a no-op rather than an error, which is what lets an AppHost keep its wiring intact while LocalStack is switched off.

It was named `WithLocalStack` first. The rename to `WithReference` was declared a breaking change in `9300539`.

## Why the public overload set has the shape it does

`AddLocalStack` exposes four non-obsolete overloads and keeps the released all-optional one as a non-error `[Obsolete]` adapter. The arrangement is forced by consumer compilers, not by taste:

- A single clean overload without `ILocalStackOptions` is ambiguous against the released one — CS0121 on SDK 8, 9 and 10 alike.
- Resolving that ambiguity with `OverloadResolutionPriorityAttribute` fails the oldest supported consumer: overload resolution priority is a C# 13 feature, and SDK 8's compiler tops out at C# 12, where it ignores the imported priority and reports CS0121. A consumer can target `net8.0` with a pre-C# 13 language version even on a newer SDK, so compiler-priority-dependent overloads are not a safe public API foundation for a package that targets `net8.0`.
- Collapsing the two longer overloads into one with an optional `configureOptions` also produced CS0121 when the callback was omitted, because the released legacy overload stayed applicable. `[Obsolete]` does not influence overload selection.

Exact forwarding overloads protect the parameterless, name-only, and explicit name/AWS-config/container call paths. The residual gap is deliberate: a named `awsConfig:` or `configureContainer:` call that omits `name` still binds the released overload and receives CS0618.

An argumentless `UseLocalStack()` was considered and rejected. The explicit `UseLocalStack(localStack)` selector keeps the enable/disable decision visible at the call site, and adding the argumentless form later stays purely additive if that judgement changes.

## The endpoint-conflict warning reads an ordered environment snapshot

`UseLocalStack()` registers the warning during `BeforeStartEvent`. For each LocalStack-enabled workload, `LocalStackEndpointConflictWarning` appends an environment callback that inspects the shared dictionary when that callback runs. It only logs: it adds, removes, and overwrites no environment values. The upstream signing limitation and its version-dependent evidence live in [Upstream Watch](../upstream-watch.md).

The callback captures a logger from `ResourceLoggerService` instead of using `EnvironmentCallbackContext.Logger`, because Aspire's container dependency discovery can evaluate environment callbacks with `NullLogger`. A warning that depends on the context logger can disappear during that evaluation.

Ordering bounds detection. Environment callbacks appended by another `BeforeStartEvent` subscriber, a later lifecycle hook, or a pipeline step after this package registers its warning can add conflicting values too late for the detector to observe. `LocalStackEndpointConflictWarningTests` pins both read-only behavior and this late-callback blind spot; absence of a warning is not proof that the final environment is conflict-free.
