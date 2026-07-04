# AWS SDK for .NET — Custom-Endpoint Signing-Region Investigation

Date: 2026-07-04

## Why This Exists

WS2's DynamoDB Streams runtime verification failed for non-us-east-1 regions: the Lambda Test Tool's streams poller signed every request for us-east-1, so LocalStack (which namespaces resources per signing region) could not find the `Urls` table deployed to eu-central-1. The playground was temporarily pinned to us-east-1. This document is the root-cause investigation that replaces guesswork with commit-level evidence, and the decision input for fixing it properly — no workarounds.

Method: a local "signing-region oracle" (a dummy HTTP listener that parses the region out of each request's `Authorization: ... Credential=.../REGION/...` header) driven by version-pinned file-based C# probes, plus source archaeology on aws/aws-sdk-net. All oracle results are deterministic and were repeat-confirmed.

## Root Cause — Two Distinct Bugs, Chained

### Bug 1 (config level): reading `ServiceURL` nulled `RegionEndpoint`

Through Core 4.0.7.4, `ClientConfig.ServiceURL`'s *getter*, when auto-resolving `AWS_ENDPOINT_URL`/`AWS_ENDPOINT_URL_<SERVICE>`/profile `endpoint_url`, invoked the property *setter*, whose side effect is `regionEndpoint = null`. Merely reading the property wiped an independently-correct region.

- Fixed in **Core 4.0.7.5** (2026-06-02) by [PR #4417](https://github.com/aws/aws-sdk-net/pull/4417) / commit `ccc133a6` — the getter now writes the backing field directly.

### Bug 2 (signing level): hardcoded us-east-1 for unparseable endpoints — **still live today**

`sdk/src/Core/Amazon.Util/Internal/RegionFinder.cs` fuzzy-parses the signing region out of the endpoint hostname. For any host it cannot parse — every custom/local endpoint: LocalStack, MinIO, `localhost:4566` — it returns a hardcoded default:

```csharp
private const string DefaultRegion = "us-east-1";
...
return _root.RegionEndpoint; // always us-east-1; never consults RegionEndpoint/AWS_REGION/profile
```

`ClientConfig.RegionEndpoint` can be perfectly correct and the signature still says us-east-1 — the signing path never looks at it.

- Fixed in **Core 4.0.9.4** (2026-06-16, commit `63a5db6b`): non-AWS endpoints fell back to `FallbackRegionFactory.GetRegionEndpoint()` (env → profile → IMDS).
- That fix caused two regressions: [#4444](https://github.com/aws/aws-sdk-net/issues/4444) (synchronous IMDS call to 169.254.169.254 hangs indefinitely on networks that black-hole it, when no region is configured) and [#4445](https://github.com/aws/aws-sdk-net/issues/4445) (~5x latency / 1.7x allocations).
- **Reverted in Core 4.0.9.7** (2026-06-22, [PR #4446](https://github.com/aws/aws-sdk-net/pull/4446)) — the hardcode is back, its unit tests were deleted with it.
- Verified still present on `main` as of 2026-07-04 (Core 4.0.100.2). **No open aws-sdk-net issue tracks the re-broken behavior.**

## Empirical Bisect (signing-region oracle; DynamoDBv2 4.0.18.5 unless noted)

| AWSSDK.Core | Config `RegionEndpoint` | Signed region |
| --- | --- | --- |
| 4.0.7.3 (Lambda Test Tool bundles this) | null (Bug 1) | us-east-1 |
| 4.0.7.5 – 4.0.9.3 | eu-central-1 | us-east-1 (Bug 2) |
| **4.0.9.4 – 4.0.9.6** | eu-central-1 | **eu-central-1 (correct)** |
| 4.0.9.7 – 4.0.100.2 (current) | eu-central-1 | us-east-1 (Bug 2 reverted back in) |

Additional verified facts:

- `AWS_REGION` vs `AWS_DEFAULT_REGION` is irrelevant: the .NET SDK reads both (verified in isolation on old and new Cores). An earlier in-repo fix based on the opposite assumption was reverted.
- Explicitly setting `cfg.RegionEndpoint` in code does not help on affected Cores — Bug 2 is downstream of config.
- **`cfg.AuthenticationRegion = "<region>"` fixes signing on ALL tested Cores, including 4.0.7.3 and 4.0.100.2** (oracle-verified). The signer honors it ahead of `RegionFinder`. This is the key to a flip-flop-immune fix.
- The profile channel (`endpoint_url` in shared config) is expected to fail identically — it feeds the same `ServiceURL`-resolution + `RegionFinder` path (inferred from source, not oracle-tested).

## Who Is Affected

- **Amazon.Lambda.TestTool (0.14.1/0.15.0, and current main)**: bundles Core 4.0.7.3 via DynamoDBv2 4.0.18.5; main has no SDK bump pending, and no upstream issue mentions this. Its DynamoDB Streams poller cannot work against region-scoped emulators outside us-east-1.
- **Any bare AWS-SDK .NET consumer** given `AWS_ENDPOINT_URL*` and a non-us-east-1 region on current SDK versions — directly relevant to WS3's planned native `AWS_ENDPOINT_URL_<SERVICE>` emission: consumers would read/write LocalStack's us-east-1 namespace while CDK-provisioned resources live in the configured region.
- **NOT affected: LocalStack.Client's default proxy mode.** It routes via `ProxyHost`/`ProxyPort` while the request URI keeps the region-bearing `*.amazonaws.com` hostname, which `RegionFinder` parses correctly. The library's proxy design is accidentally immune to Bug 2 — worth stating in consumer docs.
- This package's env emission itself is spec-correct; the defect is entirely in the SDK's signing path.

## Fix Options

Decision (2026-07-04): upstream issue/PR filing (options 1-2) is **deferred** — filing should be a structured, separately-prepared effort, not a same-day action. The repro package (oracle + bisect matrix + this document) stays ready for whenever that happens. Options 3-4 are applied: the constraint is documented as a known issue in README/CHANGELOG and tracked as an upstream watch in the roadmap.

1. **aws-sdk-net issue (report the live regression).** No open issue exists post-revert. We hold a minimal deterministic repro (oracle + version matrix) and can propose a regression-safe re-fix: prefer the client's already-resolved `RegionEndpoint`/`AuthenticationRegion` in the custom-endpoint signing path instead of walking `FallbackRegionFactory` (whose IMDS tail caused #4444). Optionally follow with a PR.
2. **aws-lambda-dotnet issue + small PR (the pragmatic near-term fix).** In the test tool's event-source client construction, set `AuthenticationRegion` from the config string's `Region` or the environment (`AWS_REGION`/`AWS_DEFAULT_REGION`). One-line-per-client, no SDK bump, oracle-verified to work on both the bundled and the current Core. Once a tool release ships it, Aspire consumers get it automatically (the tool updater installs the minimum-or-newer version; `LambdaEmulatorOptions.OverrideMinimumInstallVersion` can force it earlier).
3. **This repo, after (2) lands:** flip the playground back to a non-default region and drop the us-east-1 pin. Until then the pin stays, explicitly labeled as upstream-blocked state — it is not a solution.
4. **WS3 documentation duty:** record that native endpoint emission meets Bug 2 on current SDKs for bare-SDK consumers, and that LocalStack.Client proxy mode does not.

## Timeline Reference

| Core | SDK release | Date | Event |
| --- | --- | --- | --- |
| 4.0.7.3 | 4.0.254.0 | 2026-05-22 | Baseline; both bugs present |
| 4.0.7.5 | 4.0.260.0 | 2026-06-02 | PR #4417 fixes Bug 1 |
| 4.0.9.4 | 4.0.271.0 | 2026-06-16 | Commit 63a5db6 fixes Bug 2 |
| 4.0.9.6 | 4.0.273.0 | 2026-06-18 | Last good version |
| 4.0.9.7 | 4.0.275.1 | 2026-06-22 | PR #4446 reverts the Bug 2 fix (issues #4444/#4445) |
| 4.0.100.2 | latest | 2026-06-29+ | Bug 2 still present; no open issue |
