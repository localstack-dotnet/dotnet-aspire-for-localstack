---
name: "S2 WS2 research re-validation spike"
description: "Priming prompt for the next agent entering dotnet-aspire-for-localstack (LocalStack.Aspire.Hosting) after the WS2 Aspire/AWS modernization research was adversarially re-validated and corrected on 2026-07-02, on top of HEAD 8fd8c99 (master) — all session output is docs-only and UNCOMMITTED. WS2 research is now evidence-complete; recommended next: WS2 design pass (🔬 → 📐) for the DynamoDB Streams event-source adapter."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "Claude Fable 5"
---

You are an engineer picking up `dotnet-aspire-for-localstack` (the `LocalStack.Aspire.Hosting` package). The previous session took the freshly authored WS2 research doc (`docs/plans/ws2-aspire-aws-modernization-research.md`, dated 2026-07-02) and ran a full adversarial re-validation spike against it: every ref, source-level claim, repo-state claim, and web-source claim was independently re-verified, and the corrections were folded back into the research doc and `docs/ROADMAP.md`. **The single most important state observation:** all of this is docs-only and **uncommitted** — `docs/plans/` is untracked and three docs are modified on `master` (HEAD `8fd8c99`), pending Deniz's commit approval. The research itself is now validated and evidence-complete; WS2's next move is design, not more research.

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-07-02` — post-WS2-re-validation)** and verify against the live repo, git log, `Directory.Packages.props`, and canonical docs before acting. For Aspire/AWS/LocalStack compatibility claims, re-verify against the `external/` upstream checkouts, not memory.

## What Just Happened

### The re-validation spike (this session's core work)

Method: verified the five `external/` checkouts inline (tag == HEAD SHA, plus tag→package-version via the checked-out `Aspire.Hosting.AWS.csproj`), then fanned out **four parallel adversarial verification agents** — (1) AWS-integrations source, (2) Aspire core source, (3) this repo's claimed state, (4) web sources — each instructed to refute claims and hunt for what the research missed. Results:

| Track | Outcome |
| --- | --- |
| Refs / versions | All five checkouts verified; `release_2026-06-19` = 13.3.1, `release_2025-10-24` = 9.3.0; all three current refs still the **latest** upstream releases as of 2026-07-02. |
| String-matched internal types | `CloudFormationReferenceAnnotation.cs` and `SQSEventSourceResource.cs` **byte-identical** between 9.3.0 and 13.3.1 → type-name matching safe; `ConstantsTests` guards it at test time. |
| Aspire core 13.1.0→13.4.6 | No production-code change needed — confirmed at source level, including `ConnectionStringAvailableEvent` ordering/multiplicity. Behavioral deltas found (env-callback caching, service-discovery key format, `PersistenceAnnotation`) verified as not intersecting this package. |
| Repo-state claims | 6/9 confirmed; 2 were *undercounts* (callback has four branches, not two; callback tests are better than a stub). |
| Web claims | All confirmed; no missed AWS blog post. |

### New decision inputs the original research lacked (now in the doc)

1. **Service-specific endpoint vars are required, not optional** — upstream's own DynamoDB-local wiring emits `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS`, and service-specific vars beat the global in AWS SDK precedence. The old open question is closed.
2. **Without our adapter the helper targets real AWS and never waits for LocalStack** — it falls through every `UseLocalStack()` branch (`LocalStackResourceBuilderExtensions.cs:103-111`), so no endpoint injection *and* no `WaitFor`. Support must go through the `WithReference` attachment path.
3. **LocalStack unified-image/auth-token transition is live since 2026-03-23** — new releases need an auth token (incl. CI); our pinned `4.12.0` stays token-free but frozen. Raises WS7 urgency and gates the WS2 integration-test image choice.
4. **DynamoDB Streams is viable on LocalStack** (all plans incl. free Hobby) but **stream config does not persist across container restarts**; Kinesis-backed with flakiness history → unit-first test sequencing confirmed.

### Corrections applied (uncommitted)

| File | What changed |
| --- | --- |
| `docs/plans/ws2-aspire-aws-modernization-research.md` | 16 edits: re-validation header; still-latest + `microsoft/aspire` note; CHANGELOG citation fix (13.3.1 entry missing at the tag — cite release body/`main`); `IResourceWithCustomWithReference` corrected (internal ATS dispatcher only); new tables *Verified Behavioral Deltas* (Aspire) and *Other Upstream Changes Verified As Not Affecting This Package* (AWS); delta table completed (9.3.1/9.3.2/9.4.1/13.0.2); DynamoDB Streams details corrected (`--no-launch-window`, config-string keys, no-`DynamoDBLocalInstance` = real-AWS case); new *Verified Constraints For The LocalStack Adapter* table; endpoint vars made required; "no plugin point — copy-the-SQS-branch across four files" note; new `## LocalStack Platform Constraints` section; repo-impact and open-question rows corrected; bottom line rewritten. |
| `docs/ROADMAP.md` | WS4 `LOCALSTack...Callback.cs` line ref `:42`→`:40`; WS5 callback-tests item corrected from "stub"; WS7 urgency raised (auth-token transition) + WS2 coupling; issue #25 disposition updated; WS2 research line notes re-validation. |
| `docs/agents/README.md`, `docs/agents/skills/aspire-source-navigation.md` | (From session start, pre-existing modifications) ignored-file-aware `external/` check guidance — reviewed, kept as-is. |

Proposed commit message (not yet approved): `docs(ws2): fold re-validation corrections into research doc and roadmap`.

### Method notes & lessons (worth reusing)

- **Adversarial "what did the research miss" prompts out-performed claim re-checking**: the two biggest findings (auth-token transition, streams persistence) came from checking what the research *didn't* look at, not from refuting what it did.
- **In Claude Code, Grep/Glob DO search the gitignored `external/` tree** (empirically verified). The ignored-path warning in the docs is defensive guidance for other harnesses — keep it, but don't let it stop you from using fast tools here.
- **Shallow clones can't cross-tag `git diff`** — diff across the two checkout trees instead (`git diff --no-index --ignore-cr-at-eol`).
- **CHANGELOG-at-tag can lag the release it ships in** (the 13.3.1 trap). Verify release claims against the GitHub release body, not the tagged CHANGELOG.
- Agent memory (Claude Code) was updated: `upstream-ref-mapping` and `aspire-source-navigation-state` now reflect the five current checkouts. Other harnesses should rely on the research doc's Local Upstream Source table instead.

## Onboarding Snapshot

- **Product:** `LocalStack.Aspire.Hosting` (namespace `Aspire.Hosting.LocalStack`) — extends `Aspire.Hosting.AWS` so AWS resources (CloudFormation/CDK/Lambda/projects) run against LocalStack, with automatic fallback to real AWS when disabled. Public API: `AddLocalStack`, `UseLocalStack` (auto-discovery), `WithReference` overloads.
- **Tooling:** .NET SDK per `global.json`; multi-target net8/9/10; **TUnit + Microsoft.Testing.Platform + NSubstitute**; Central Package Management; strict analyzers + warnings-as-errors.
- **Layout:** `src/Aspire.Hosting.LocalStack/`, `tests/` (Unit + Integration, TUnit), `playground/` (provisioning CFN+CDK, lambda URL-shortener), `docs/`.

## Current State You Should Assume Until Verified

- **HEAD** (`master`): `8fd8c99` — "fix: route CDK asset-upload S3/STS clients to LocalStack (#29) (#30)".
- **Worktree expectation**: **dirty** — modified: `docs/ROADMAP.md`, `docs/agents/README.md`, `docs/agents/skills/aspire-source-navigation.md`; untracked: `docs/plans/` (the WS2 research doc) and this pickup. Commit awaits approval.
- **Pinned versions** (`Directory.Packages.props`): `Aspire.Hosting 13.4.6`, `Aspire.Hosting.AWS 13.3.1`, `LocalStack.Client 2.0.0`, `AWSSDK.Core 4.0.9.6`. Default LocalStack image tag `4.12.0`.
- **Tests (TUnit)**: not run this session (docs-only).
- **Active workstream**: **WS2 🔬** — research complete and re-validated; design not started. WS0/WS1/WS1.5/WS9 ✅.
- **Local-only state (gitignored, will NOT travel):** five `external/` checkouts, all verified tag==HEAD:
  - `external/aspire/v13.4.6` (`87fe259`) + comparison `v13.1.0` (`8a4db17`)
  - `external/aws-integrations/release_2026-06-19` (`b8f5c04`, = 13.3.1) + comparison `release_2025-10-24` (`83e36c3`, = 9.3.0)
  - `external/localstack-dotnet-client/v2.0.0` (`873efbf`)

## Recommended Next Step

**0. Land the pending docs first** (blocking, tiny): get Deniz's approval and commit the uncommitted docs (proposed message above). Everything below assumes the research doc is the stable reference.

**1. WS2 design pass (🔬 → 📐) — the main arc.** Design the DynamoDB Streams event-source adapter.

- **Pre-flight:** invoke `aspire-source-navigation`; read the research doc end-to-end, especially *Verified Constraints For The LocalStack Adapter* and *LocalStack Platform Constraints*; skim the SQS twin path in `src/` (`Internal/Constants.cs`, `LocalStackResourceBuilderExtensions.cs:86-111`, `Internal/LocalStackConnectionStringAvailableCallback.cs`, `Internal/LocalStackResourceConfigurator.cs`) and upstream `external/aws-integrations/release_2026-06-19/src/Aspire.Hosting.AWS/Lambda/DynamoDBStreamsEventSource*.cs`.
- **The design is constraint-bound, not open-ended:** route through `WithReference`/`WaitFor`; emit `AWS_ENDPOINT_URL` + `AWS_ENDPOINT_URL_DYNAMODB` + `AWS_ENDPOINT_URL_DYNAMODB_STREAMS` + credentials/region; pin the env-callback last-writer-wins ordering in tests; extend the `ConstantsTests` reflection guard; unit-first, integration later. It is a copy-the-SQS-branch change across four files — there is no plugin point.
- **First sub-decision needing Deniz:** integration-test image strategy — stay on pinned pre-transition `4.12.0` (token-free, frozen) vs. adopt the unified image with auth-token plumbing in CI (couples WS7/issue #25).
- **Acceptance:** a design section (or short plan doc under `docs/plans/`) covering the constraint table, the playground sample plan (the lambda playground already exposes a `UrlsTableName` output — natural extension), and the test strategy; roadmap WS2 row flipped to 📐. Implementation itself is approval-gated separately.

**2. Alternative (smaller, if Deniz wants a quick win):** WS4 bug #24 (`LOCALSTACK_HOST` host/internal port conflation, `LocalStackConnectionStringAvailableCallback.cs:40`) under TDD.

Talk to Deniz before committing to which one. Default to working on `master`; no commit without explicit approval.

## Mandatory Grounding (read in this order)

1. `AGENTS.md` — approval gate, communication style, skills index, Aspire routing.
2. `docs/ROADMAP.md` — workstreams, **Status & Plan Mapping** table, Inbox.
3. `docs/plans/ws2-aspire-aws-modernization-research.md` — the validated WS2 evidence base; treat its constraint tables as the design contract.
4. `docs/agents/README.md` + `docs/agents/KNOWN_ISSUES.md`.
5. `docs/agents/skills/aspire-source-navigation.md` — and **invoke the skill** before compatibility-sensitive work.
6. `src/Aspire.Hosting.LocalStack/` (start: `LocalStackResourceBuilderExtensions.cs`, `Internal/`).
7. `external/{aspire,aws-integrations,localstack-dotnet-client}/{ref}/` — local upstream source (see Current State for refs).

## Locked Policy Recap

- No commit without explicit "go / apply / proceed / başla / yap". Conventional Commits; **no AI attribution trailers**.
- No starting features / refactoring prod / build-CI / release changes without approval. Docs-only + read-only discovery are fine.
- Versions live in `Directory.Packages.props` (CPM) — never hand-edit versions into `.csproj`. Strict analyzers + warnings-as-errors; run slopwatch after LLM-authored code/test changes when available.
- Compat work: resolve from `Directory.Packages.props`, cross-check `external/` — never upstream default branches. `external/` is local-only; never commit it.
- **One backward-compatible release**; `[Obsolete]` over breaking changes. WS3 decoupling is AppHost-internal only — consumers keep `LocalStack__*` env **and** gain native `AWS_ENDPOINT_URL_<SERVICE>`. Deniz authors `localstack-dotnet-client` — client compatibility is non-negotiable.
- TUnit filters with `--treenode-filter`; plain `--filter`/`--nologo` silently run zero tests — confirm total > 0.
- New raw ideas → the **Inbox / Untriaged** section of `docs/ROADMAP.md`.

## Final Steering Note

The research phase for WS2 is genuinely done — validated twice over, with the misses found and folded in. Resist the pull to re-research; the constraint tables in the research doc are the design contract, and the SQS path is the proven twin to mirror. The one thing that can still change the design's shape is the integration-test image decision (pinned `4.12.0` vs unified image + token) — surface it to Deniz early rather than designing around an assumption. Land the pending docs commit first, then take WS2 from 🔬 to 📐 in one focused pass.
