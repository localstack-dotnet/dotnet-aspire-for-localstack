# Session Pickup Prompt — Template

> **How to use:** at the end of a session that closed a non-trivial wave, copy this file to `s<N>-<short-summary>.prompt.md` (e.g. `s1-roadmap-foundation.prompt.md`). Replace every `{{placeholder}}` with session-specific content. Drop any section that has no content. Length is not a virtue — useful pickups are 100-200 lines, not 250+.
>
> **When NOT to write a pickup:** doc-only edits, single-bug fixes, or routine refreshes. Pickups are for handing off **architectural waves, multi-commit refactors, roadmap/planning landings, or genuinely stateful in-flight work** (including local-only `external/` checkouts) that the next session needs primed context to continue.

---

## Frontmatter (replace every field)

```yaml
---
name: "S{{N}} {{short-summary-of-what-just-closed}}"
description: "Priming prompt for the next agent entering dotnet-aspire-for-localstack (LocalStack.Aspire.Hosting) after {{wave-or-task}} landed at {{sha}} on {{date}}. {{one-sentence-state-summary}}. Recommended next: {{A | B | C}}."
argument-hint: "Optional focus area, constraints, or reason to override the recommended next step"
agent: "agent"
model: "{{model identifier — e.g. Claude Opus 4.8}}"
---
```

---

## Body skeleton

### Opening paragraph

One paragraph stating: who you are (engineer entering the repo), what just landed (wave + sha + date), and the one most important state observation. No fluff.

### `## First Principle`

> Treat every claim here as **current-as-of-authoring (`{{date}}` — `{{wave-tag}}`)** and verify against the live repo, git log, `Directory.Packages.props`, and canonical docs before acting. For Aspire/AWS/LocalStack compatibility claims, re-verify against the `external/` upstream checkouts, not memory.

This block stays nearly verbatim across pickups — it's the standing reminder that pickup prompts go stale.

### `## What Just Happened`

The detailed change ledger. Use sub-headers for distinct waves / concerns. For each:

- **What changed** — file paths, signatures, removed / added types
- **Why** — 1-2 lines anchored to a settled decision / roadmap workstream / research finding
- **Verification** — build/test outcomes (TUnit), slopwatch results, manual checks

For mechanical changes (version bumps, rename waves, doc consolidations) prefer **tables**: denser than prose, easier for the next agent to skim. Include any inline deferrals or known carry-forward items at the end of each sub-section.

This is the longest section. Don't pad it; do not omit anything load-bearing for the next agent.

### `## Onboarding Snapshot` *(optional — drop if the previous pickup already covered it well and nothing shifted)*

Quick re-orientation. Brief stack reminder and the locked-decisions list. **Most of this is durable across pickups** — copy from the previous pickup and adjust where things shifted. Don't re-derive from scratch every session.

Durable stack facts for this repo:

- **Product:** `LocalStack.Aspire.Hosting` (namespace `Aspire.Hosting.LocalStack`) — a .NET Aspire hosting integration that extends `Aspire.Hosting.AWS` so AWS resources run against LocalStack, with automatic fallback to real AWS when disabled.
- **Tooling:** .NET SDK per `global.json`; multi-target net8/9/10; **TUnit + Microsoft.Testing.Platform + NSubstitute**; Central Package Management (`Directory.Packages.props`); strict analyzers + warnings-as-errors via shared MSBuild.
- **Source layout:** `src/Aspire.Hosting.LocalStack/`, `tests/` (Unit + Integration), `playground/` (provisioning CFN/CDK + lambda), `docs/`.

### `## Current State You Should Assume Until Verified`

A short bullet list with concrete current values:

- **HEAD** (`{{branch}}`): `{{sha}}` — `{{commit-summary}}`
- **Worktree expectation**: `{{clean | unstaged X}}`
- **Pinned versions** (`Directory.Packages.props`): `{{Aspire.Hosting X, Aspire.Hosting.AWS Y, LocalStack.Client Z}}`
- **Tests (TUnit)**: `{{N/N passed / M skipped | "not run this session"}}`
- **Active workstream**: `{{WS# + the next steps in its docs/plans plan, or "none in-flight"}}`
- **Local-only state**: `{{anything gitignored the next session may need — e.g. external/ upstream clones at which refs, scratchpad extractions}}`
- **Background/parallel work**: `{{state}}`

Always verifiable, always specific. Vague status entries are worse than absent ones.

### `## Recommended Next Step`

1-3 numbered options. Each option:

- **Name + classification** — e.g. "lightweight, well-scoped" / "multi-session arc" / "optional"
- **Pre-flight steps** — what to read / verify before starting (incl. which skill to invoke)
- **Specific files / sections to touch** — concrete paths
- **Acceptance criteria** — what "done" looks like

End with: "Talk to Deniz before committing to which one." Default to working on the current branch; do not branch unless there is a concrete reason. No commit without explicit "go / apply / proceed / başla / yap" (AGENTS.md approval gate).

### `## Mandatory Grounding (read in this order)`

A numbered read order. Adjust per scope, but the canonical core stays:

1. `AGENTS.md` — canonical repository contract: communication style, approval gate, skills index, Aspire routing (`CLAUDE.md` and `.github/copilot-instructions.md` are relay-only).
2. `README.md` + `docs/CONFIGURATION.md` — product behavior and configuration surface.
3. `docs/ROADMAP.md` — phased backlog, **Status & Plan Mapping table** (the progress tracker), todo triage, and the **Inbox / Untriaged** capture spot.
4. `docs/plans/{{active-workstream-plan}}` — the live plan for the workstream in flight.
5. `docs/agents/README.md` (agent harness guide) + `docs/agents/KNOWN_ISSUES.md` (triage hints).
6. `docs/agents/skills/aspire-source-navigation.md` — canonical skill body; **invoke the `aspire-source-navigation` skill** before any Aspire/AWS/LocalStack compatibility-sensitive work.
7. The relevant `src/` / `tests/` files for the scope.
8. `external/{aspire,aws-integrations,localstack-dotnet-client}/{ref}/` — local upstream checkouts for source-level compatibility (gitignored; see local-only state).

Skip entries the next session does not need; do not pad with everything.

### `## Locked Policy Recap`

Curated invariants list. Most carry over verbatim from session to session. This section is for the **most-likely-to-be-tempting-to-violate** rules in the upcoming work, not a full mirror of `AGENTS.md`.

- No commit without explicit "go / apply / proceed / başla / yap". Conventional Commits (`feat|fix|docs|test|refactor|build|ci|chore`); **no AI attribution trailers**.
- Do not start a feature, refactor production code, change build/CI, or run release/publish commands without approval. Docs-only edits, link fixes, and read-only discovery are allowed.
- Package versions live in `Directory.Packages.props` (Central Package Management) — **never hand-edit versions into individual `.csproj` files**.
- Strict analyzers + warnings-as-errors are on. Run `slopwatch analyze --fail-on warning ...` after LLM-authored code/test changes when available.
- Compatibility work: resolve versions from `Directory.Packages.props`, then cross-check against the `external/` checkouts — **do not use upstream default branches**. `external/` is local-only/gitignored; never commit upstream clones.
- `AGENTS.md` is canonical; `CLAUDE.md` and `.github/copilot-instructions.md` stay relay-only; the skill's canonical body lives in `docs/agents/skills/`, native `SKILL.md` files are thin relays.
- **Roadmap release philosophy:** everything ships in **one release**, maximally backward-compatible; prefer `[Obsolete]` (remove next release) over breaking changes. The AppHost-decoupling workstream is AppHost-internal only — consumers keep receiving `LocalStack__*` env vars (LocalStack.Client.Extensions support is non-negotiable) **and** gain native `AWS_ENDPOINT_URL_<SERVICE>`.
- Skill instructions are kept general-purpose; concrete version→ref values live in agent memory / `docs/ROADMAP.md`, not hardcoded into skills.

### `## Final Steering Note`

1-2 paragraphs. Closing direction. Hint at the natural rhythm for the next session — not a hard mandate. End short, specific, motivating.

---

## Drift sensitivity per section

| Section | Drift sensitivity |
| --- | --- |
| Frontmatter | Session-specific — rewrite |
| Opening paragraph | Session-specific — rewrite |
| First Principle | Stable |
| What Just Happened | Session-specific — rewrite |
| Onboarding Snapshot | Mostly stable; adjust per shift |
| Current State | Session-specific — rewrite |
| Recommended Next Step | Session-specific — rewrite |
| Mandatory Grounding | Stable; adjust if doc topology shifts |
| Locked Policy Recap | Stable; source from `AGENTS.md` |
| Final Steering Note | Session-specific — rewrite |

When the doc topology shifts (new plan doc, renamed roadmap section, retired doc), update Mandatory Grounding in this template too — that's the entry point the next pickup author copies from.

---

## Authoring discipline

- **Verify before claiming.** Don't write "X is at Y state" without `git log --oneline -5` + a quick repo grep. Pickup prompts are read by future agents who will treat your claims as current.
- **Anchor to commit SHAs.** Every "just landed" claim should reference a specific commit.
- **Flag local-only state.** Gitignored artifacts (`external/` clones, scratchpad extractions) do not travel with the repo — if the next session needs them, say so explicitly, with the exact refs.
- **Drop sections that don't apply.** A pickup with no test runs should not have a "Tests: TBD" line — drop the bullet.
- **Don't re-derive `AGENTS.md`.** If the next session needs it, point at it; do not paraphrase.
- **Sign off with a concrete next-action recommendation, not five parallel futures.** Default + alternatives, not buffet.
- **Update the `docs/ROADMAP.md` Status table in the same change** — the pickup is the deep handover; the roadmap Status row is the permanent index entry.
