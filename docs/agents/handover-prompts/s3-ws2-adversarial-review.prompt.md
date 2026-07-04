---
name: "S3 WS2 implementation adversarial review"
description: "Priming prompt for the next agent entering dotnet-aspire-for-localstack after WS2 (DynamoDB Streams event-source support + async QR playground) was implemented, runtime-verified, and pushed on 2026-07-04 as feature/ws2-dynamodb-streams (27 commits, full history) mirrored by feature/ws2-dynamodb-streams-squashed (single commit 53c3680, tree-identical). Mission: adversarially review EVERYTHING — challenge every assumption, re-verify every empirical claim, report findings before Deniz's own review and the PR to master."
argument-hint: "Optional focus area (package code / playground / upstream-bug claims / docs) or constraints"
agent: "agent"
model: "Claude Fable 5"
---

You are an adversarial reviewer entering `dotnet-aspire-for-localstack` (the `LocalStack.Aspire.Hosting` package). The previous session implemented WS2 end to end and pushed two branches on 2026-07-04: `feature/ws2-dynamodb-streams` (27 commits, the true history — read it for archaeology) and `feature/ws2-dynamodb-streams-squashed` (single commit `53c3680` off `master@8fd8c99`, tree-identical — review the changeset there). **Nothing is merged; a PR to master waits on review.** Your mission is NOT to continue the work: it is to attack it. Challenge every assumption, re-reproduce every empirical claim, and report what does not survive.

## First Principle

> Treat every claim below as **current-as-of 2026-07-04** and re-verify against the live repo, both branches, `Directory.Packages.props`, and the `external/` upstream checkouts (verified refs: `aspire/v13.4.6`, `aws-integrations/release_2026-06-19` = Aspire.Hosting.AWS 13.3.1, `localstack-dotnet-client/v2.0.0`). For compatibility-sensitive claims use the `aspire-source-navigation` skill. The prior session made — and had to retract — confidently wrong claims; assume you can catch more.

## What Landed (review scope = the squashed commit's diff)

- **Package**: `UseLocalStack()` detects `Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource` by type-name string and attaches it to LocalStack; `LocalStackConnectionStringAvailableCallback` dispatches it to a new configurator emitting `AWS_ENDPOINT_URL`, `AWS_ENDPOINT_URL_DYNAMODB`, `AWS_ENDPOINT_URL_DYNAMODB_STREAMS`, credentials, `AWS_DEFAULT_REGION` (unconditional — an earlier don't-clobber guard was added, then removed). `UseLocalStack()` fails fast (`DistributedApplicationException`) when `IDynamoDBLocalResource` is present.
- **Playground (lambda)**: QR generation moved from synchronous (`Format=qr` + deleted `qrUrl` response) to CDC: `UrlsTable` gets `Stream = NEW_IMAGE`, new `QrCodeGeneratorLambda` consumes `WithDynamoDBStreamsEventSource(...)`, `GET /{slug}/qr` (served by a second Lambda resource `QrStatusLambda` on the same Redirector project) returns 404/202/302-to-object-URL, URL built by a restored `S3UrlService` (not presigned), new `LocalStack.Lambda.Frontend` control-room page, region pinned to `us-east-1` with an explanatory comment.
- **Tests**: unit 483/483 (net8/9/10) including fail-fast + configurator coverage; integration 22/22 including a new async-CDC polling test and a model-level fail-fast test via `DistributedApplicationTestingBuilder` against the real playground entry point.
- **Docs**: `docs/plans/ws2-dynamodb-streams-adapter-design.md` (living spec incl. post-execution decisions), `...-implementation-plan.md` (executed, with amendment notes), `docs/plans/aws-sdk-signing-region-investigation.md` (upstream-bug evidence), `docs/plans/ws10-aspire-ecosystem-integration-research.md`, README Known Limitations, CHANGELOG `[Unreleased]`, ROADMAP (WS2 ✅, WS3/WS6/WS7 insights, WS10 added), `playground/lambda/README.md` rewritten.

## The Claims Register — attack each one

1. **aws-sdk-net signing-region regression** (the big one): claim = `RegionFinder.FindRegion` hardcodes us-east-1 for non-fuzzy-parseable endpoints; fixed in Core 4.0.9.4 (commit `63a5db6b`), reverted in 4.0.9.7 (PR #4446, due to issues #4444/#4445), still broken at 4.0.100.2; `AuthenticationRegion` bypasses it on ALL Cores; `AWS_REGION` vs `AWS_DEFAULT_REGION` is irrelevant (.NET SDK reads both). Evidence: `docs/plans/aws-sdk-signing-region-investigation.md`. The empirical oracle (dummy HTTP listener parsing the `Authorization` credential-scope region + version-pinned file-based C# probes) lived in a session scratchpad and is GONE — **rebuild it from the doc's description and re-run the matrix yourself.** Single-machine, single-run-per-version data deserves skepticism.
2. **us-east-1 is the only working region for the streams poller today** — follows from claim 1 plus "LocalStack namespaces tables AND streams per signing region; DYNAMODB_SHARE_DB shares only tables, not Kinesis-backed streams". Re-verify the LocalStack-side claims; they were established during live debugging, not from LocalStack source.
3. **API Gateway emulator holds one route per Lambda resource** (second `WithReference` silently overwrites) — evidence: `APIGatewayExtensions.cs:101-104` in the verified checkout + runtime 404. The chosen fix (second `QrStatusLambda` resource on the same project) was a mid-execution unilateral call, later sanctioned. Challenge both the reading of upstream and whether the fix is the right shape.
4. **LocalStack.Client default registration is proxy-mode** (ProxyHost/ProxyPort + RegionEndpoint, no ServiceURL) so data calls work but generated URL strings (presigns) leak the AWS regional host; proxy-mode is accidentally immune to claim 1. Evidence: `Session.cs` in the v2.0.0 checkout + one live observation.
5. **Fail-fast rationale**: DynamoDB Local + LocalStack = competing state stores; no env-reachable writer of the service-specific endpoint keys remains, so the removed guard was dead code (the helper resource is upstream-internal and unreachable for user `WithEnvironment`). Try to construct a counterexample.
6. **Type-name string matching against Aspire.Hosting.AWS 13.3.1 internals is stable** — guarded by reflection tests, but the guard only proves presence, not behavioral equivalence.
7. **The playground demo claims** in README/CHANGELOG (202→302 ~1-2s, PNG served, frontend panels) — verified live several times, but on one machine. `aspire start` + curl it yourself.
8. **Aspire tooling claims** in `docs/agents/README.md` (official skills as `aspire:<name>`, MCP server behavior, catalog prefix-filter findings in the WS10 doc) — spot-check the load-bearing ones.

## Mistakes Already Made And Corrected (calibrate your skepticism)

- Claimed ".NET SDK does not read AWS_DEFAULT_REGION", shipped a fix on that hypothesis, later disproved it empirically and reverted (`cc6fbda` → reverted in `5497d42`).
- Cited a 2020-closed issue (aws-sdk-net #1692) as evidence for a 2026 behavior.
- Tried `DYNAMODB_SHARE_DB=1` as the region fix; it only moved the failure from `DescribeTable` to `DescribeStream` (commits `9ecada1` → `93add1e` tell the story).
- Presigned URLs were the original design; runtime showed the proxy-mode leak; reverted to Deniz's `S3UrlService` pattern (`dda865c`).
- The existing integration suite was never run during plan execution; Deniz's question exposed a deterministically failing test asserting the removed sync-QR contract (`50fc82e` fixed it).
- Two agent-kill notifications were misattributed to Deniz; the profile-channel experiment (claims-register #1 sidebar: "profile channel fails identically") is **INFERRED, never tested** — flag it as such wherever repeated.

## Your Task

1. Onboard: `AGENTS.md` first (approval gates bind you too — review only, no production fixes without explicit approval), `docs/agents/README.md` for capability routing, then the four plan/investigation docs above and the squashed diff (`git show 53c3680` or `git diff master...feature/ws2-dynamodb-streams-squashed`).
2. Re-verify: run the unit suite (`dotnet test --project tests/...Unit.Tests... --no-launch-profile`; TUnit/MTP — filters after `--`, `--treenode-filter` single pattern, zero-tests-run = false green), the integration suite (Docker required), and a live playground pass (`aspire start`, Aspire MCP server is registered; region us-east-1). Rebuild the signing-region oracle and re-run at least: tool-bundled Core 4.0.7.3, 4.0.9.6, 4.0.9.7, latest, and the AuthenticationRegion bypass.
3. Attack the claims register item by item. For each: verdict CONFIRMED / REFUTED / UNVERIFIABLE with evidence. Also review code quality of the squashed diff itself (the prior final review was done by the same session that wrote the code — you are the first independent set of eyes).
4. Report findings-first, severity-ordered, file:line, per AGENTS.md review style. Deniz will read your report before his own review and the PR to master.

## State Snapshot

Branches pushed to origin: `feature/ws2-dynamodb-streams` (history), `feature/ws2-dynamodb-streams-squashed` (`53c3680`, tree-identical — verified via empty `git diff` between the branches). `master` untouched at `8fd8c99`. Working tree clean. Deferred by explicit decision: upstream issue filing (aws-sdk-net regression, aws-lambda-dotnet route/SDK — repro material ready in the investigation doc), DDB Streams integration tests on newer LocalStack images (WS7 gate), WS10 catalog strategy (CommunityToolkit vs upstream proposal). The session-local SDD ledger (`.superpowers/sdd/progress.md`, gitignored) has per-task history if the checkout survives.
