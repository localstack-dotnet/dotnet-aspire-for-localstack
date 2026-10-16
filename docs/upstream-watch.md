# Upstream Watch

Date: 2026-10-16

Defects that live in upstream code, not in this package. Each entry records the evidence, what this repository does in the meantime, and the trigger that closes it. A workaround here is a holding position with an exit, never a design.

## Live: the Lambda Test Tool bundles an AWS SDK that signs custom-endpoint requests for `us-east-1`

**Effect on this repository.** The Lambda Test Tool's DynamoDB Streams poller cannot reach a region-scoped emulator outside `us-east-1`, because LocalStack namespaces resources per signing region. The Lambda playground is therefore pinned to `us-east-1`, and that pin is upstream-blocked state rather than a solution.

**Root cause — two bugs, chained.** `Amazon.Util.Internal.RegionFinder` fuzzy-parses the signing region out of the endpoint hostname. For any host it cannot parse — every custom or local endpoint, LocalStack and `localhost:4566` included — it returned a hardcoded `us-east-1` and never consulted `RegionEndpoint`, `AWS_REGION`, or the profile. A second, earlier bug compounded it: through Core 4.0.7.4, reading `ClientConfig.ServiceURL` invoked its own setter while auto-resolving `AWS_ENDPOINT_URL*`, and that setter nulls `regionEndpoint` — so merely reading the property wiped an independently correct region. That one was fixed in Core 4.0.7.5 by [PR #4417](https://github.com/aws/aws-sdk-net/pull/4417).

**The signing bug has flip-flopped.** It was fixed in Core 4.0.9.4 by falling back to `FallbackRegionFactory` (environment, then profile, then IMDS), which introduced two regressions — [#4444](https://github.com/aws/aws-sdk-net/issues/4444), a synchronous IMDS call that hangs on networks that black-hole `169.254.169.254`, and [#4445](https://github.com/aws/aws-sdk-net/issues/4445), roughly 5x latency — and was reverted in Core 4.0.9.7 by [PR #4446](https://github.com/aws/aws-sdk-net/pull/4446), deleting its unit tests with it.

Measured with a signing-region oracle (a listener that parses the region out of each request's `Authorization: ... Credential=.../REGION/...` header), driven by version-pinned probes:

| AWSSDK.Core | Config `RegionEndpoint` | Signed region |
| --- | --- | --- |
| 4.0.6.1 – 4.0.7.4 (the range the Lambda Test Tool bundles) | null — the `ServiceURL` bug | `us-east-1` |
| 4.0.7.5 – 4.0.9.3 | `eu-central-1` | `us-east-1` |
| 4.0.9.4 – 4.0.9.6 | `eu-central-1` | `eu-central-1`, correct |
| 4.0.9.7 – 4.0.100.2 | `eu-central-1` | `us-east-1`, the revert |

**The SDK side is fixed. Re-measured 2026-10-16 against this repository's current pin:**

| AWSSDK.DynamoDBv2 | AWSSDK.Core | Signed region |
| --- | --- | --- |
| 4.0.21.6 | 4.0.100.2 | `us-east-1` — the revert, reproduced |
| 4.0.21.6 | **4.0.102.11** | **`eu-central-1` — correct** |
| 4.0.107.3 | **4.0.102.11** | **`eu-central-1` — correct** |

Same oracle, same `AWS_REGION=eu-central-1`, custom `ServiceURL` pointing at a local listener. The third row holds DynamoDBv2 constant against the second, so the variable is Core — which is where `RegionFinder` lives. The fix re-landed somewhere between 4.0.100.2 and 4.0.102.11. That matches both the maintainer's report that the AWS .NET team said they would address it and an assembly review noting the custom-endpoint fallback now resolves environment and profile while excluding instance metadata, which is the shape that avoids the IMDS hang (#4444) that forced the first revert.

When writing such a probe: set `RegionEndpoint` **after** `ServiceURL` and the latter is silently cleared — the setters are mutually exclusive, and the request then goes to real AWS instead of the local listener. Drive the region from `AWS_REGION` and set only `ServiceURL`.

**The blocker has moved, and the playground pin stays.** The Lambda Test Tool is a global `dotnet` tool: it runs in its own process against an `AWSSDK.Core` it packages itself, under `tools/net8.0/any` and `tools/net10.0/any`. The Core this repository resolves governs the AppHost and the workloads, never the tool. And the tool's own copy is old on both the version Aspire installs and the newest one published:

| Tool version | Bundled `AWSSDK.Core` | Carries |
| --- | --- | --- |
| 0.14.1 — what `Aspire.Hosting.AWS` 13.7.2 installs | 4.0.6.1 | both bugs |
| 0.16.1 — newest published, 2026-09-23 | 4.0.7.3 | both bugs |

Both sit below 4.0.7.5, so the `ServiceURL`-read bug is present as well as the signing bug. Upgrading the tool by hand therefore does not help today.

`Aspire.Hosting.AWS` does not track the newest tool. `Constants.DefaultLambdaTestToolVersion` is `"0.14.1"`, and `LambdaBeforeStartEventHandler` treats it as a floor: on a machine with nothing installed it runs `dotnet tool install -g Amazon.Lambda.TestTool --version 0.14.1`, and on a machine that already has something newer it leaves it alone. Verified in the shipped assembly rather than the default branch — the UTF-16 string heap of `Aspire.Hosting.AWS.dll` 13.7.2 holds `0.14.1` next to `tool install -g Amazon.Lambda.TestTool --version`. The lever for overriding it is `OverrideMinimumInstallVersion` on the Lambda emulator annotation, which is what lets a consumer adopt a fixed tool without waiting for Aspire to raise the constant.

When checking for a fixed tool, `Amazon.Lambda.TestTool 1.0.0` on NuGet is a decoy: it sorts highest but is **unlisted** and is the legacy netcoreapp2.1 library package that depends on `AWSSDK.Core 3.3.104.25`, not the tool at all. The tool line is the `0.x` one.

The reason for the `us-east-1` pin has therefore narrowed from "the AWS SDK signs the wrong region" to "the Lambda Test Tool packages a stale SDK, and Aspire installs an even staler one than the newest available".

**Facts that stay true across versions.**

- `cfg.AuthenticationRegion = "<region>"` fixes signing on every Core tested, including 4.0.7.3 and 4.0.100.2 — the signer honors it ahead of `RegionFinder`. This is the flip-flop-immune fix, and the shape any upstream patch should take.
- Setting `cfg.RegionEndpoint` in code does not help on affected versions; the signing bug sits downstream of config.
- `AWS_REGION` versus `AWS_DEFAULT_REGION` is irrelevant — the .NET SDK reads both.
- **LocalStack.Client's default proxy mode is not affected.** It routes through `ProxyHost`/`ProxyPort` while the request URI keeps the region-bearing `*.amazonaws.com` hostname, which `RegionFinder` parses correctly. The proxy design is accidentally immune.
- This package's environment emission is itself correct. The defect is entirely in the SDK's signing path, which is why the package warns about proxy/native coexistence instead of emitting native endpoint variables.

**Who else is exposed.** Any bare AWS SDK for .NET consumer given `AWS_ENDPOINT_URL*` and a non-`us-east-1` region on an affected Core: it reads and writes LocalStack's `us-east-1` namespace while provisioned resources live in the configured region.

**Provenance of the fix.** The maintainer relayed that the AWS .NET team said they would address this; the measurement above confirms they did, on the Core side. The tool-side gap is the part they will not have seen, because it lives in a package they ship separately.

**Triggers.**

1. **Amazon.Lambda.TestTool bumps its bundled `AWSSDK.Core` past 4.0.100.2, or sets `AuthenticationRegion` on its event-source clients** → pick the fixed tool up through `OverrideMinimumInstallVersion`, lift the playground's `us-east-1` pin, and drop the limitation from `README.md` and `CHANGELOG.md`. This is the only thing left in the way, and it is worth reporting to the AWS .NET team as a follow-up to the Core fix: that fix does not reach Aspire's DynamoDB Streams path while the tool carries its own copy of the SDK, and Aspire's install floor of 0.14.1 means a clean machine gets an even older one than the newest tool offers.
2. **A consumer reports wrong-region behavior from a bare SDK client** → check their resolved `AWSSDK.Core` against the tables above before assuming this package is at fault.

## Live: LocalStack requires an auth token on current images

The package pins `localstack/localstack:4.12.0`, the last image that starts without `LOCALSTACK_AUTH_TOKEN`. Upstream left semantic versioning after `4.14.0` and now publishes calendar versions; the current tag is `2026.10.0`. Consumers who need a newer image pass the token through `AdditionalEnvironmentVariables`.

Whether `4.13.x` or `4.14.0` still start without a token is **unverified**: upstream's own documentation contradicts itself, with the auth-token page calling the token mandatory to start the container and the docker-images page describing only a warning and disabled Pro features. The repository's own runtime verification establishes only that `4.12.0` works token-free.

**Trigger.** Pull `4.14.0` and start it with no token. That settles whether a newer token-free pin exists before any decision about moving to calendar versions or modelling the token as a secret parameter.

## Closed: Aspire annotation-collection race on Linux CI

Integration tests on the Linux CI job could fail en masse, every test at `0ms`, with `InvalidOperationException: Collection was modified` thrown from `LocalStackLambdaFixture.InitializeAsync`. The cause was an unsynchronized reader meeting a start-time writer: `DcpExecutor.RunApplicationAsync` enumerated annotation collections from parallel per-resource tasks while this package's `ConnectionStringAvailableEvent` callback added environment annotations to the same collections. In Aspire 13.4.6, `ResourceAnnotationCollection` was a plain `Collection<IResourceAnnotation>` with no synchronization. Slower two-core hosted runners widened the overlap, which is why it only ever surfaced on Linux CI.

Fixed upstream by [PR #18259](https://github.com/microsoft/aspire/pull/18259), which rebuilt the collection on an `ImmutableArray<T>` backing store with lock-free reads and locked writes, shipped in Aspire 13.5.0. Verified closed on 2026-10-16: at tag `v13.6.1`, which this repository now pins, `ResourceAnnotationCollection` is backed by `ThreadSafeAnnotationList`. Treat a recurrence as a new defect rather than this one.

The design-quality item the investigation raised separately is not closed and does not depend on this: moving environment-annotation registration out of the connection-string-available event to model-construction time. It stays in `docs/ROADMAP.md`, and it changes callback precedence — this package's callbacks currently run last *because* registration happens at event time.
