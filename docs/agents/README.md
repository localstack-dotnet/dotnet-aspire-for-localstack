# Agent Harness Guide

Date: 2026-07-04

This directory contains repository-specific guidance for AI coding agents.

## Source Of Truth

`AGENTS.md` is the canonical always-on contract. Harness-specific files relay to it or expose native discovery points, but they do not own policy.

`AGENTS.md` intentionally stays compact and capability-oriented. Keep mandatory repository policy there; keep harness-native names, local setup details, marketplace repair notes, LSP wiring, and skill relay maintenance in this adapter guide.

Changes to `AGENTS.md`, approval gates, capability routing, skill triggers, or harness adapter behavior are policy/infrastructure changes. They require explicit approval even when the edit is Markdown-only.

| File | Purpose |
| --- | --- |
| `AGENTS.md` | Canonical repository contract |
| `CLAUDE.md` | Claude Code relay to `AGENTS.md` |
| `.github/copilot-instructions.md` | GitHub Copilot relay to `AGENTS.md` |
| `docs/agents/README.md` | Harness adapter guide and capability mapping (this file) |
| `docs/agents/KNOWN_ISSUES.md` | Agent-facing known notes and triage hints |
| `docs/agents/skills/aspire-source-navigation.md` | Canonical project skill content |
| `.claude/skills/aspire-source-navigation/SKILL.md` | Claude Code native skill relay |
| `.opencode/skills/aspire-source-navigation/SKILL.md` | OpenCode native skill relay |
| `.github/skills/aspire-source-navigation/SKILL.md` | GitHub Copilot / VS Code Agent Skills relay |

## Harness Notes

Claude Code discovers project skills under `.claude/skills/{skill-name}/SKILL.md`.

OpenCode discovers project skills under `.opencode/skills/{skill-name}/SKILL.md`. OpenCode loads skill files at session start, so restart OpenCode after changing `.opencode/skills/**`; the current running session will not discover newly added project skills.

GitHub Copilot in VS Code supports repository instructions through `.github/copilot-instructions.md` and Agent Skills under `.github/skills/`.

Do not create `.vscode` skill folders. That is not a canonical Agent Skills location for this repository.

## Capability Mapping

`AGENTS.md` routes by capability so the contract stays harness-neutral. Resolve each capability to the current harness's native invocation before acting.

Tier meanings:

- **Tier 0** — process discipline. Lightweight discipline (diagnosis, review, verification) applies broadly per the First Decision Flow; heavyweight orchestration (brainstorming, multi-step planning, TDD branch loops, subagent execution) runs only when Deniz explicitly requests it. Process skills are tooling, not policy.
- **Tier 1** — required when triggered for this repo's Aspire/LocalStack package work; invoke before acting when installed or shipped.
- **Tier 2** — optional by judgment; use when it materially improves correctness, safety, test quality, or diagnostics.
- **Tier 3** — local-only convenience; use when present, never assume fresh checkouts have it.
- **Out of scope** — do not use unless this repo adds that technology or Deniz explicitly asks.

This repo ships exactly one project skill: `aspire-source-navigation`. Its canonical body lives in `docs/agents/skills/aspire-source-navigation.md`, with thin native relays under `.claude/skills/`, `.opencode/skills/`, and `.github/skills/`.

Claude Code uses plugin-qualified names. Copilot CLI exposes installed skill IDs directly through the running harness's skill list. OpenCode exposes skill frontmatter names, which depend on the local install; the names below reflect the established local convention. Official Microsoft skills carry `ms-dotnet-*` prefixes in OpenCode to avoid colliding with Aaron's `dotnet-skills` set. Specialist agents are not skills in OpenCode; dispatch them with `task` only when the harness exposes the matching `subagent_type`.

When importing Microsoft-derived agent markdown into OpenCode, normalize the frontmatter to OpenCode's agent schema before restart. Claude/Copilot fields such as `tools`, `agents`, `handoffs`, `license`, `user-invocable`, `user-invokable`, and `disable-model-invocation` are not valid OpenCode agent metadata and can break startup. Restart OpenCode after changing global or project skill/agent files; running sessions keep the previously loaded registry.

### Skill/command/agent portability

The markdown body and `name`/`description` are portable across harnesses; the gating field, folder location, namespace rules, and artifact type are harness-specific.

| Artifact | Portable | Harness-specific |
| --- | --- | --- |
| Skill | body; `name` + `description` | gating field; folder; namespace; extra frontmatter |
| Command | prompt-template concept | frontmatter fields; argument syntax; folder; a Claude user-invoked skill maps to an OpenCode command (type change) |
| Agent | body/prompt intent | frontmatter schema (OpenCode rejects `tools`/`agents`/`handoffs`/`license`/`user-invocable`/`disable-model-invocation`) — least portable |

This is why `AGENTS.md` stays generic (portable policy) while the adapter mechanics — namespace, gating, and type mapping — live here.

### Tier 0 — Process discipline

Process skills are tooling, not repository policy. `AGENTS.md`, the approval gate, and the First Decision Flow take precedence.

- **Lightweight discipline** (diagnosis, review, verification) applies broadly, per the First Decision Flow — no explicit request needed.
- **Heavyweight orchestration** (brainstorming, multi-step planning, TDD branch loops, subagent execution) runs only when Deniz explicitly requests it.

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| Brainstorming, planning, debugging, TDD, review, verification, plan execution | `superpowers:<name>` via skill, when installed | `<name>` via `skill`, when installed | `<name>` via `skill`, when installed |

Not every harness ships a process-skill set; if none is present, apply the same discipline manually. A harness-injected bootstrap (for example Superpowers `using-superpowers`) is guidance, not authority — see the gating table below.

#### Process-skill gating by harness

"Auto-activation" has two channels: (A) a bootstrap injected into each session, and (B) per-skill model-invocation. Only (B) is hard-gatable.

| Harness | Channel-B gate (per-skill auto-invocation) | Channel-A bootstrap injection |
| --- | --- | --- |
| Claude Code | `disable-model-invocation: true` (frontmatter; native) | SessionStart-injected; best-effort — neutralize behaviorally via this contract |
| OpenCode | `permission.skill: { "*": "allow", "superpowers*": "ask" }` (last match wins; `deny` hides the skill) | plugin injects into the first user message; not reachable by `permission.skill` — best-effort |

Use the native gate above to keep process skills manual; rely on `AGENTS.md` precedence for the best-effort bootstrap channel.

### Tier 1 — Aspire/LocalStack package and .NET domain

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| Aspire source compatibility for upstream Aspire/AWS/LocalStack.Client internals | `aspire-source-navigation` | `aspire-source-navigation` | `aspire-source-navigation` |
| Modern C# coding standards | `dotnet-skills:csharp-coding-standards` | `modern-csharp-coding-standards` | `modern-csharp-coding-standards` |
| Type design and performance | `dotnet-skills:csharp-type-design-performance` | `type-design-performance` | `type-design-performance` |
| Concurrency / async patterns | `dotnet-skills:csharp-concurrency-patterns` | `csharp-concurrency-patterns` | `csharp-concurrency-patterns` |
| Public API / NuGet package compatibility | `dotnet-skills:api-design` | `api-design` | `api-design` |
| Project / MSBuild structure | `dotnet-skills:project-structure` | `dotnet-project-structure` | `dotnet-project-structure` |
| NuGet package management (CPM) | `dotnet-skills:package-management` | `package-management` | `package-management` |
| Dependency injection patterns | `dotnet-skills:microsoft-extensions-dependency-injection` | `dependency-injection-patterns` | `dependency-injection-patterns` |
| Options/configuration patterns | `dotnet-skills:microsoft-extensions-configuration` | `microsoft-extensions-configuration` | `microsoft-extensions-configuration` |
| Serialization contracts | `dotnet-skills:serialization` | `serialization` | `serialization` |
| Slopwatch quality gate | `dotnet-skills:slopwatch` | `dotnet-slopwatch` | `dotnet-slopwatch` |
| Aspire explicit configuration | `dotnet-skills:aspire-configuration` | `aspire-configuration` | `aspire-configuration` |
| Aspire ServiceDefaults | `dotnet-skills:aspire-service-defaults` | `aspire-service-defaults` | `aspire-service-defaults` |
| Aspire integration testing | `dotnet-skills:aspire-integration-testing` | `aspire-integration-testing` | `aspire-integration-testing` |

`aspire-source-navigation` is narrow by design. Do not invoke it just because a task mentions Aspire; invoke it when the task depends on upstream Aspire/AWS/LocalStack internals, package-version alignment, source-level API shape, or a compatibility conclusion. For read-only explanation questions, inspect this repository's docs/code first and invoke the skill only when upstream version-specific evidence is needed.

For package version updates, use the package-management capability for Central Package Management and `dotnet` command mechanics. Use `aspire-source-navigation` for compatibility evidence and upstream source checks. If the guidance overlaps, package-management governs how packages are edited; source navigation governs whether the version/API behavior is compatible.

### Tier 2 — Test and coverage (TUnit on Microsoft.Testing.Platform)

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| Running / filtering tests | `dotnet-test:run-tests` | `run-tests` | `ms-dotnet-test-run-tests` |
| Test anti-pattern audit | `dotnet-test:test-anti-patterns` | `test-anti-patterns` | `ms-dotnet-test-test-anti-patterns` |
| Test gap (mutation-style) analysis | `dotnet-test:test-gap-analysis` | `test-gap-analysis` | `ms-dotnet-test-test-gap-analysis` |
| Assertion quality analysis | `dotnet-test:assertion-quality` | `assertion-quality` | `ms-dotnet-test-assertion-quality` |
| Test generation | `dotnet-test:code-testing-agent` | `code-testing-agent`, then `dotnet-test:code-testing-generator` via `task` | `ms-dotnet-test-code-testing-agent`, then `code-testing-generator` via `task` |
| Find untested sources | `dotnet-test:find-untested-sources` | `find-untested-sources` | `ms-dotnet-test-find-untested-sources` |
| Mock usage audit (NSubstitute/Moq/FakeItEasy) | Not available (unpublished plugin) | Not available | `ms-dotnet-experimental-exp-mock-usage-analysis` |
| Test maintainability / duplicate boilerplate audit | Not available (unpublished plugin) | Not available | `ms-dotnet-experimental-exp-test-maintainability` |
| Coverage + CRAP analysis | `dotnet-test:coverage-analysis`, `dotnet-test:crap-score` | `coverage-analysis`, `crap-score` | `ms-dotnet-test-coverage-analysis`, `ms-dotnet-test-crap-score` |
| Broad test-suite audit (agent) | `dotnet-test:test-quality-auditor` agent | `dotnet-test:test-quality-auditor` via `task` | `test-quality-auditor` via `task` |
| Snapshot testing (Verify) for manifest/config/API-surface output | `dotnet-skills:snapshot-testing` | `snapshot-testing` | `snapshot-testing` |
| Container-backed integration tests (Docker) — alternative to Aspire integration tests, use only when the Aspire path does not fit | `dotnet-skills:testcontainers-integration-tests` | `testcontainers-integration-tests` | `testcontainers-integration-tests` |

This repo uses TUnit on Microsoft.Testing.Platform. Avoid false-green filters: plain `--filter` / `--nologo` can silently run zero tests; prefer `dotnet test --project <csproj>` and TUnit-compatible filtering, and confirm the reported total is greater than zero.

OpenCode notes: use reference-only helper skills such as `ms-dotnet-test-filter-syntax`, `ms-dotnet-test-platform-detection`, `ms-dotnet-test-frameworks`, `ms-dotnet-test-code-testing-extensions`, and `ms-dotnet-test-test-analysis-extensions` only when a parent test workflow asks for framework lookup data. Do not invoke those helpers as standalone task handlers.

Claude Code / Copilot CLI notes: `dotnet-experimental` exists in the marketplace repo but may be unpublished or absent from a given harness. Treat the experimental mock-usage and test-maintainability rows as OpenCode-local unless verified in the running harness.

### Tier 2 — Performance, diagnostics, and specialist agents

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| Microbenchmarking (BenchmarkDotNet or custom benchmarks) | `dotnet-diag:microbenchmarking` | `microbenchmarking` | `ms-dotnet-diag-microbenchmarking` |
| Performance anti-pattern analysis | `dotnet-diag:analyzing-dotnet-performance` | `analyzing-dotnet-performance` | `ms-dotnet-diag-analyzing-dotnet-performance` |
| Performance optimization (agent) | `dotnet-diag:optimizing-dotnet-performance` agent | `dotnet-diag:optimizing-dotnet-performance` via `task` | `optimizing-dotnet-performance` via `task` |
| Benchmark design (agent) | `dotnet-skills:dotnet-benchmark-designer` agent | `dotnet-benchmark-designer` via `task` | `dotnet-benchmark-designer` via `task` |
| Performance analysis of measured data (agent) | `dotnet-skills:dotnet-performance-analyst` agent | `dotnet-performance-analyst` via `task` | `dotnet-performance-analyst` via `task` |
| Concurrency / race analysis (agent) | `dotnet-skills:dotnet-concurrency-specialist` agent | `dotnet-concurrency-specialist` via `task` | `dotnet-concurrency-specialist` via `task` |
| Trace / dump collection | `dotnet-diag:dotnet-trace-collect`, `dotnet-diag:dump-collect` | `dotnet-trace-collect`, `dump-collect` | `ms-dotnet-diag-dotnet-trace-collect`, `ms-dotnet-diag-dump-collect` |
| Decompile assemblies when source is unavailable | `dotnet-skills:ilspy-decompile` | `ilspy-decompile` | `ilspy-decompile` |

Performance work requires measured data before optimization claims. Prefer source navigation over decompilation when local upstream checkouts under `external/` are available. OpenTelemetry instrumentation work in playground ServiceDefaults or Lambda samples may use the OpenCode-local `OpenTelemetry-NET-Instrumentation` skill when present; do not treat it as package-source routing for ordinary hosting changes.

### Tier 2 — Meta / maintenance

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| Maintain `AGENTS.md` / this file's capability index | `dotnet-skills:skills-index-snippets` | `skills-index-snippets` | `skills-index-snippets` |
| Working-diff code review (findings-first, severity-ordered) | `code-review` (harness built-in) | `code-review` via `task` | `codex-review` via `task` when present |
| Broad read-only exploration or bounded research | Harness-native explore/general agent if installed | Harness-native explore/general agent if installed | `explore` or `general` via `task` when useful |

### Tier 2 — Official Aspire skills and Aspire MCP server (playground run/debug)

Official Microsoft Aspire skills and MCP server are local harness setup, not committed project infrastructure. They require Aspire CLI 13.3+ (`aspire agent mcp`).

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| AppHost lifecycle routing + safety guardrails (`aspire start`, never `dotnet run` on AppHosts) | `aspire:aspire` | `aspire` | `aspire` |
| Start/stop/restart/wait/inspect playground AppHost resources | `aspire:aspire-orchestration` | `aspire-orchestration` | `aspire-orchestration` |
| Resource logs, traces, metrics, dashboard telemetry | `aspire:aspire-monitoring` | `aspire-monitoring` | `aspire-monitoring` |
| Runtime resource state/logs/traces/commands over MCP | `aspire` MCP server (`aspire agent mcp`, stdio; tools surface as `mcp__aspire__*`) | `aspire` MCP server (`aspire agent mcp`, stdio; user `~/.copilot/mcp-config.json`) | `aspire` MCP server (`aspire agent mcp`, stdio; local `opencode.jsonc`) |

- The MCP server only discovers AppHosts launched with `aspire start` from the workspace directory. In-process `DistributedApplicationTestingBuilder` AppHosts used by integration tests are invisible to it — test debugging stays log/debugger-based.
- These skills/tools are for *consuming* Aspire (running and debugging playground AppHosts). They do not replace `aspire-source-navigation` for upstream source-compatibility work; on conflict, verified package source wins.
- The bundle also ships `aspire-init` and `aspireify` (not for this repo — AppHosts already exist) and `aspire-deployment` (approval-gated and real-AWS targeted; LocalStack playgrounds do not deploy).
- Set up each harness locally and update only that harness's cells after verifying the native skill IDs and MCP status.

### Tier 3 — Local-only

| Capability | Claude Code | Copilot CLI | OpenCode |
| --- | --- | --- | --- |
| OpenCode local model routing | Not applicable | Not applicable | `subagent-model-routing` via `skill` when present |

### Out of scope

Do not invoke these unless the repo adds the technology or Deniz explicitly asks:

- **Akka.NET**: `akka-*` skills and `akka-net-specialist` — no Akka.NET here.
- **Email/MJML/Mailpit**: `mjml-email-templates`, `verify-email-snapshots`, `mailpit-integration` — no email stack here.
- **EF Core / SQL database performance**: `efcore-patterns`, `database-performance` — this package configures AWS resources and LocalStack endpoints; it does not use EF/SQL.
- **Playwright / Blazor UI**: `playwright-blazor-testing`, `playwright-ci-caching` — no browser UI test surface in this package.
- **Marketplace publishing**: `marketplace-publishing` — this repo is not publishing skills/agents to a marketplace.
- **MSTest-specific work**: `ms-dotnet-test-writing-mstest-tests` / `writing-mstest-tests` — this repo uses TUnit, not MSTest.
- **VSTest-to-MTP migration**: `ms-dotnet-test-migration-migrate-vstest-to-mtp` — this repo is already on TUnit/Microsoft.Testing.Platform.
- **Testability/static-wrapper migrations**: `ms-dotnet-test-detect-static-dependencies`, `ms-dotnet-test-generate-testability-wrappers`, `ms-dotnet-test-migrate-static-to-wrapper`, `testability-migration` — only use if Deniz asks for a dedicated testability migration.
- **Reactive extensions**: `r3-reactive-extensions` — no R3/Rx usage here.
- **Generators / DocFX / unrelated platform work**: `roslyn-incremental-generator-specialist`, `docfx-specialist`, crash-symbolication skills, and `dotnet-devcert-trust` are out of scope unless the repo adds that concern.
- **Academic-only test smell taxonomy**: `ms-dotnet-test-test-smell-detection` and `ms-dotnet-test-test-tagging` are narrow tools; use only when explicitly requested.

Official Microsoft **Aspire** skills (`aspire`, `aspireify`, `aspire-orchestration`, `aspire-monitoring`, `aspire-deployment`, `aspire-init`) are a separate source, not part of `dotnet-agent-skills`. They are installed per harness — see the "Official Aspire skills and Aspire MCP server" Tier 2 section above for the roster, MCP wiring, and usage limits. Deployment remains approval-gated.

Availability is not activation. Skills and process bootstraps do not grant automatic process authority; invoke the mapped skill or dispatch the mapped specialist agent when the trigger applies, and invoke heavyweight process workflows only when Deniz explicitly requests them. If a mapped capability is not loaded in the current harness, skip optional rows or ask before installing, changing harness configuration, or substituting another tool. Do not invent an ID.

Copilot VS Code differs from Copilot CLI: repository skills under `.github/skills/` are discoverable, but external Superpowers, Aaron `dotnet-skills`, and Microsoft `dotnet-agent-skills` availability depends on the active Copilot/agent environment. Resolve them from the running harness instead of assuming Claude-style plugin names.

### Per-Developer OpenCode Setup

Some OpenCode conveniences are intentionally local-only and ignored by git: `opencode.jsonc`, `.opencode/agents/`, and `.opencode/skills/subagent-model-routing/`. They may define model-routed agents such as `deepseek-light`, `codex-coder`, `glm-hardcore`, `codex-review`, or local slash commands, but they are not shipped project infrastructure.

When present, `subagent-model-routing` can help choose among local OpenCode agents and providers. Do not treat it as a required project skill, and do not assume another checkout exposes the same `subagent_type` names.

External general-purpose skill packs (for example Matt Pocock's) may be installed locally on Claude and OpenCode. Where they overlap a repo capability, prefer this repo's own capability for repo work: use the project `/research` and `/review` flows and Superpowers systematic-debugging over their `matt-research`, `matt-code-review`, `matt-diagnosing-bugs`, and `matt-tdd` equivalents. On OpenCode, `permission.skill: { "matt-*": "ask" }` is the backstop, and adapted Matt skills keep `matt-*` prefixes; Claude resolves them by its own qualified name.

## Claude Code .NET Skill Marketplaces

Two .NET skill marketplaces are used, and their names are intentionally distinct:

- `Aaronontheweb/dotnet-skills` declares marketplace name `dotnet-skills` (the `dotnet-skills:*` convention skills).
- Microsoft's `dotnet/skills` declares marketplace name `dotnet-agent-skills` (the `dotnet-test:*` / `dotnet-diag:*` procedure skills).

Do not run `claude plugin marketplace add dotnet/skills` as a blind repair step. Claude may key the marketplace by a repo-path-derived name (`dotnet-skills`), which collides with Aaron's install directory and silently repoints the `dotnet-skills` registry entry to Microsoft's repo. The result is an orphaned Aaron plugin: its clone/cache may remain on disk but stop resolving.

If the registry is already wrong, back up the files first, then repair directly:

1. In `~/.claude/plugins/known_marketplaces.json` and `~/.claude/settings.json` -> `extraKnownMarketplaces`, point `dotnet-skills` to `Aaronontheweb/dotnet-skills`.
2. Add a separate `dotnet-agent-skills` entry pointing to `dotnet/skills`.
3. Ensure Microsoft's repo is cloned to `~/.claude/plugins/marketplaces/dotnet-agent-skills`.
4. Install Microsoft plugins with `claude plugin install <plugin>@dotnet-agent-skills`.
5. Verify with `claude plugin marketplace list`; both marketplaces must show their correct source repos.

## Semantic Code Navigation (LSP) By Harness

`AGENTS.md` carries the decision rule (Rider MCP first, then the harness headless LSP, then text search). Harness wiring:

- **Rider MCP:** Preferred when Rider is running and its MCP tools are present — solution index, ReSharper analysis, and semantic refactors.
- **Claude Code:** The headless LSP is the `csharp-lsp` plugin (community `csharp-ls`). The official Microsoft `dotnet` plugin's Roslyn LSP does not load on Claude Code yet — Claude reads `lspServers` only from `.claude-plugin/plugin.json` while the plugin ships its manifest at the root (dotnet/skills#846). Do not hand-edit vendored plugin caches to force it; when #846 ships it loads automatically.
- **Copilot CLI:** The `dotnet@dotnet-agent-skills` plugin's `lsp.json` works out of the box (launches `roslyn-language-server` via `dnx`).
- **OpenCode:** Use native OpenCode LSP config; OpenCode does not consume Claude/Copilot plugin `lsp.json`. Restart OpenCode after config changes.

## Skill Maintenance

The canonical skill body lives in `docs/agents/skills/aspire-source-navigation.md`. Native `SKILL.md` files should stay small and point back to the canonical document.

When changing agent guidance, update `AGENTS.md` only for mandatory cross-harness policy. Update this file for adapter mechanics. Update native skill relay files only when their trigger description or canonical path changes.

Use the `skills-index-snippets` capability to keep this capability index consistent when skills are added, retired, renamed, or re-tiered. Change the roster deliberately and keep this file aligned with `AGENTS.md`.

When changing the skill:

1. Update the canonical document first.
2. Update native relay files only if the trigger description or canonical path changes.
3. Verify no relay copied the full canonical body.
4. Verify OpenCode-compatible frontmatter exists in `.opencode/skills/aspire-source-navigation/SKILL.md`.
5. Tell OpenCode users to restart their session if they need the updated skill loaded.
6. After restart, verify the OpenCode skill can be loaded with `skill aspire-source-navigation`.

## Local Upstream Sources

Source-level Aspire compatibility checks should prefer local upstream checkouts:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/localstack-dotnet-client/{ref}/
```

Resolve `{ref}` from package versions in `Directory.Packages.props` and verified upstream tags/releases. The `external/` tree is ignored by git, so use an ignored-file-aware check such as `Test-Path external`, `git ls-files --others --ignored --exclude-standard external/`, or a direct directory listing before concluding local source is missing. Do not commit upstream clones.
