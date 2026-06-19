---
name: "S1 Roadmap + source-navigation foundation"
description: "Priming prompt for the next agent entering dotnet-aspire-for-localstack (LocalStack.Aspire.Hosting) after the roadmap, source-navigation skill upgrade, external/ upstream checkouts, and WS9 doc consolidation landed on branch docs/roadmap-building (HEAD 3f04f4f) on 2026-06-19. The project now has a 9-workstream roadmap with progress tracked in docs/ROADMAP.md. Recommended next: WS1 — full package update (the gate for WS2)."
argument-hint: "Optional focus area or reason to override WS1 as the next step"
agent: "agent"
model: "Claude Opus 4.8"
---

You are an engineer picking up `dotnet-aspire-for-localstack` (the `LocalStack.Aspire.Hosting` package). The previous session built the project's first prioritized roadmap, hardened the `aspire-source-navigation` skill, set up local upstream source checkouts under `external/`, and consolidated/retired the gitignored `internal-docs/` (WS9). All of it landed as docs commits on branch `docs/roadmap-building` (HEAD `3f04f4f`, 2026-06-19). **The single most important state observation:** no production code changed this session — this was pure planning + tooling + docs. The roadmap (`docs/ROADMAP.md`) is now the source of truth for what to do next, and **WS1 (full package update) is the priority and the gate for everything compatibility-related.**

## First Principle

> Treat every claim here as **current-as-of-authoring (`2026-06-19` — post-WS9)** and verify against the live repo, git log, `Directory.Packages.props`, and canonical docs before acting. For Aspire/AWS/LocalStack compatibility claims, re-verify against the `external/` upstream checkouts, not memory.

## What Just Happened

| Commit | What | Why |
| --- | --- | --- |
| `c6dccb8` | `aspire-source-navigation` skill: added a general version→ref resolution method + approval-gated checkout setup recipe (`docs/agents/skills/aspire-source-navigation.md`) | The AWS-integration repo uses date tags, not semver; the skill lacked a method for that and for cloning checkouts |
| `2d1cecb` | Created `docs/ROADMAP.md` — 9 workstreams, todo.md triage, KNOWN_ISSUES/issue mappings, verbatim original vision | First consolidated, prioritized backlog for the project |
| `f50e0fa` | Added the Status & Plan Mapping table to the roadmap; documented `docs/ROADMAP.md` + `docs/plans/` in `AGENTS.md` | Progress is tracked in-document; no separate progress file |
| `3f04f4f` | WS9: consolidated + deleted `internal-docs/`; added Inbox section; wrote `docs/plans/ws9-docs-consolidation.md` | internal-docs was 8-month-old, mostly-shipped history; all live items captured in the roadmap first |

**Source-level groundwork (local-only, see Current State):** three upstream repos were cloned under `external/` at the refs matching the *currently pinned* package versions, to enable source-accurate compatibility checks against the existing code.

**Deep-dive findings worth carrying forward** (verified against `src/` this session — anchor future work to these):

- **LocalStack.Client coupling is two-layered.** AppHost-internal: `AddLocalStack` consumes `ILocalStackOptions`/`LocalStackOptions`/`ConfigOptions`/`SessionOptions` and builds the CloudFormation client via `SessionStandalone...CreateClientByImplementation<AmazonCloudFormationClient>()` (`Internal/LocalStackResourceConfigurator.cs`). Consumer-side: projects get `LocalStack__*` env (needs LocalStack.Client.Extensions), while the SQS-event-source path already emits native `AWS_ENDPOINT_URL`. → WS3.
- **#24 bug is localized:** `Internal/LocalStackConnectionStringAvailableCallback.cs:42` sets `LOCALSTACK_HOST` to the host-facing `{host}:{port}`; internal consumers must target the internal port `4566` regardless of a custom host `Port`.
- **String-typename matching against AWS internals** (`Internal/Constants.cs`: `SQSEventSourceResource`, `CloudFormationReferenceAnnotation`) is version-sensitive — top risk for the 9.3→13.x AWS-integration jump.
- **Temporary direct pins**: `MessagePack` + `AWSSDK.Core` are pinned in `Aspire.Hosting.LocalStack.csproj` to dodge a vuln restore failure (KNOWN_ISSUES); decoupling should remove them.
- **Drift:** default image tag is `4.12.0` (`Container/LocalStackContainerImageTags.cs`) vs `4.10.0` in docs; `<Version>13.1.0</Version>` is hand-maintained in the csproj.

## Onboarding Snapshot

- **Product:** `LocalStack.Aspire.Hosting` (namespace `Aspire.Hosting.LocalStack`) — extends `Aspire.Hosting.AWS` so AWS resources (CloudFormation/CDK/Lambda/projects) run against LocalStack, with automatic fallback to real AWS when disabled. Public API: `AddLocalStack`, `UseLocalStack` (auto-discovery), `WithReference` overloads.
- **Tooling:** .NET SDK per `global.json` (10.0.100); multi-target net8/9/10; **TUnit + Microsoft.Testing.Platform + NSubstitute**; Central Package Management; strict analyzers + warnings-as-errors.
- **Layout:** `src/Aspire.Hosting.LocalStack/` (~1.2k LOC, 16 files), `tests/` (Unit ~114 / Integration ~24, TUnit), `playground/` (provisioning CFN+CDK, lambda URL-shortener), `docs/`.

## Current State You Should Assume Until Verified

- **HEAD** (`docs/roadmap-building`): `3f04f4f` — "docs: consolidate internal-docs into roadmap and retire the folder (WS9)" (plus possibly a later handover-docs commit). Remote may be behind local; check `git status -sb`.
- **Worktree expectation**: clean (all session work committed).
- **Pinned versions** (`Directory.Packages.props`): `Aspire.Hosting 13.1.0`, `Aspire.Hosting.AWS 9.3.0`, `LocalStack.Client 2.0.0`, `AWSSDK.Core 4.0.9.6`. Default LocalStack image tag `4.12.0`.
- **Tests (TUnit)**: not run this session (no code changed).
- **Active workstream**: none in-flight. WS9 is ✅. **WS1 is the recommended next** — no plan written yet.
- **Local-only state (gitignored, will NOT travel):** `external/` upstream checkouts exist at verified refs —
  - `external/aspire/v13.1.0` (HEAD `8a4db17`)
  - `external/aws-integrations/release_2025-10-24` (= `Aspire.Hosting.AWS 9.3.0`, HEAD `83e36c3`)
  - `external/localstack-dotnet-client/v2.0.0` (HEAD `873efbf`)
  These match the *currently pinned* versions. WS1 (updating to latest) will need **new** checkouts at the new target refs.

## Recommended Next Step

**WS1 — Full package update (multi-step; the gate for WS2).** Talk to Deniz before committing to it.

- **Pre-flight:** invoke the `aspire-source-navigation` skill; read `docs/ROADMAP.md` (WS1 + WS2 sections) and `Directory.Packages.props`. Resolve the latest target refs (Aspire ~`13.4.x`, `Aspire.Hosting.AWS` `13.x` — note the major realign from 9.x; LocalStack.Client) and clone new `external/` checkouts at those refs.
- **Touch:** `Directory.Packages.props` (version bumps), `src/Aspire.Hosting.LocalStack/Aspire.Hosting.LocalStack.csproj` (`<Version>`), and re-validate the string-typename matching in `Internal/Constants.cs` against the new `Aspire.Hosting.AWS` source.
- **Acceptance:** solution builds with warnings-as-errors clean; TUnit unit + integration tests pass (integration needs Docker); slopwatch clean if available. Produce the WS1 plan at `docs/plans/ws1-package-update.md` and flip the roadmap Status row.
- **Carry-forward into WS2:** enumerate the `Aspire.Hosting.AWS` 9.3→13.x breaking changes you hit — that list seeds WS2.

Alternative (smaller, if Deniz wants a quick win first): pick off a WS4 bug (#24 `LOCALSTACK_HOST` or the trailing-space `AWS_REGION`) under TDD. Out of scope unless asked: WS8 features, #18 Azure.

## Mandatory Grounding (read in this order)

1. `AGENTS.md` — approval gate, communication style, skills index, Aspire routing.
2. `README.md` + `docs/CONFIGURATION.md` — product + config surface.
3. `docs/ROADMAP.md` — workstreams, **Status & Plan Mapping** table, todo triage, Inbox.
4. `docs/agents/README.md` + `docs/agents/KNOWN_ISSUES.md`.
5. `docs/agents/skills/aspire-source-navigation.md` — and **invoke the skill** before WS1/WS2 work.
6. `src/Aspire.Hosting.LocalStack/` (start: `LocalStackResourceBuilderExtensions.cs`, `Internal/`).
7. `external/{aspire,aws-integrations,localstack-dotnet-client}/{ref}/` — local upstream source.

## Locked Policy Recap

- No commit without explicit "go / apply / proceed / başla / yap". Conventional Commits; **no AI attribution trailers**.
- No starting features / refactoring prod / build-CI / release changes without approval. Docs-only + read-only discovery are fine.
- Versions live in `Directory.Packages.props` (CPM) — never hand-edit versions into `.csproj`. Strict analyzers + warnings-as-errors; run slopwatch after LLM-authored code/test changes.
- Compat work: resolve from `Directory.Packages.props`, cross-check `external/` — never upstream default branches. `external/` is local-only; never commit it.
- **One backward-compatible release**; `[Obsolete]` over breaking changes. WS3 decoupling is AppHost-internal only — consumers keep `LocalStack__*` env **and** gain native `AWS_ENDPOINT_URL_<SERVICE>`. Deniz authors `localstack-dotnet-client` (millions of users) — client compatibility is non-negotiable.
- New raw ideas → the **Inbox / Untriaged** section of `docs/ROADMAP.md` (todo.md is retired).

## Final Steering Note

The planning phase is done; the next arc is execution, and it starts at the foundation. WS1 is mechanical in spirit but the `Aspire.Hosting.AWS` 9.3→13.x jump is the real substance — treat the breaking-change list you produce as a first-class deliverable, because it becomes WS2. Re-clone `external/` at the new refs before trusting any compatibility judgment, lean on the `aspire-source-navigation` skill, and keep the roadmap Status table honest as you go.
