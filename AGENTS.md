# Agent Instructions

Operating rules for LLM/code agents working in this repository.

## Communication Style

- Be direct, practical, and clear.
- Challenge decisions when needed; do not yes-person your way into bad architecture.
- Prefer small correct changes over broad refactors.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best matches the current message.

## Approval Gate

### Do Not Without Explicit Approval

- Start coding a new feature.
- Refactor production code.
- Modify build system behavior.
- Change CI/CD pipelines.
- Run deployment, publish, or package-release commands.
- Commit, amend, push, or create a PR.

Approval phrases include `go`, `apply`, `proceed`, `basla`, and `yap`.

### Allowed Without Additional Approval

- Documentation-only edits.
- Broken internal link fixes.
- Minor comment improvements that do not change behavior.
- Read-only discovery commands and dry-run checks.

### Before Any Commit

Present a concise change summary and proposed Conventional Commit message, then ask for approval.

Commit messages must use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, or `chore`. Do not add AI attribution trailers.

## Project Context

This repository builds `LocalStack.Aspire.Hosting`, a NuGet package in the `Aspire.Hosting.LocalStack` namespace.

Keep this file lightweight. Read change-prone project facts from their source files instead of copying them here:

- SDK and test runner: `global.json`
- Target frameworks, analyzers, and Central Package Management: `Directory.Build.props`
- Package versions: `Directory.Packages.props`
- Package metadata and project references: project files under `src/`, `tests/`, and `playground/`
- Runtime behavior: source code and tests first, then README/docs

Repository layout for onboarding:

- `src/`: package source
- `tests/`: unit and integration tests
- `playground/`: sample AppHosts and workloads
- `docs/`: project documentation
- `docs/ROADMAP.md`: phased, prioritized roadmap and in-document progress tracker
- `docs/plans/`: detailed per-workstream deep-dive plans, linked from the roadmap (created as each workstream is planned)
- `docs/agents/`: agent-facing guidance and known notes

## Docs-First Workflow

Before non-trivial work, inspect the relevant docs and code. For runtime behavior, code wins over docs.

Recommended reading order:

1. `README.md`
2. `docs/CONFIGURATION.md`
3. `AGENTS.md`
4. `docs/agents/README.md`
5. The relevant source or test file

Change hygiene:

- Update docs when behavior, topology, package support, or harness expectations change.
- Prefer consolidation over new docs when an existing doc is the natural home.
- Research docs must include a date.
- Keep `docs/ROADMAP.md` current: it is the single source for backlog and progress. Update the Status column as workstreams move, and link each workstream's plan in `docs/plans/` when its deep-dive lands. Do not start a separate progress file.
- Code comments and XML docs must be self-contained; do not reference specs, plans, phases, or external file paths from code comments.

## Quality Notes

- Use the standard .NET restore/build/test flow appropriate to the files changed.
- Integration tests require Docker and LocalStack-compatible runtime conditions.
- Package versions live in `Directory.Packages.props`; do not hand-edit package versions into individual project files.
- Strict analyzers and warnings-as-errors are enabled through shared project configuration.
- Documentation-only changes do not require build/test unless they add or change commands that should be validated.
- If Slopwatch is available after LLM-authored code, project, or test changes, run:

```powershell
slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"
```

## Harness Portability

- `AGENTS.md` is the canonical repository contract.
- Harness-specific instructions and skill files are adapters, not policy sources; keep relay files lightweight and pointed back to this contract.
- Harness adapter locations, capability mapping, LSP wiring, and local-only setup notes live in `docs/agents/README.md`.
- Never commit secrets, OAuth tokens, personal machine paths, local MCP config, or personal OpenCode model-routing config.

## Source Navigation For Aspire Compatibility

Use `aspire-source-navigation` before changing or reviewing work that depends on Aspire hosting internals, `Aspire.Hosting.AWS`, LocalStack.Client behavior, package version alignment, or source-level compatibility.

The skill must resolve package versions from `Directory.Packages.props`, then cross-check those versions against local upstream checkouts under `external/`. Do not silently use upstream default branches for compatibility-sensitive checks.

Preferred local checkout layout uses refs derived from package versions and verified upstream tags/releases:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/localstack-dotnet-client/{ref}/
```

The `external/` tree is local-only and ignored by git. GitHub MCP is allowed for tag/ref discovery, release verification, or fallback reads when local source is unavailable; it is not the default source-reading path.

Official Microsoft Aspire skills may describe newer Aspire versions than this repo uses. Verify API shape against `Directory.Packages.props` and local upstream source before applying newer guidance.

## Skills And Agents Used In This Project

Skills and specialist agents encode workflow discipline and retrieval-led reasoning. Use them by **capability**, not by memorized harness names:

1. Identify the capability needed from the routing sections below.
2. Resolve the harness-native invocation from `docs/agents/README.md`.
3. Invoke the harness-native skill or dispatch the harness-native specialist agent before acting.
4. If an optional capability is unavailable, skip it or ask before replacing it with a different tool.

Availability is not activation. Except for the Superpowers bootstrap, skills do not run automatically; matching triggers require explicit skill invocation or specialist-agent dispatch.

Capability tiers:

- **Tier 0** — bootstrap/process discipline; follow when injected by the harness.
- **Tier 1** — required when triggered for this repo's Aspire/LocalStack package work; invoke before acting when installed or shipped.
- **Tier 2** — optional by judgment; use when it materially improves correctness, safety, test quality, or diagnostics.
- **Tier 3** — local-only convenience; use when present, never assume fresh checkouts have it.
- **Out of scope** — do not use unless this repo adds that technology or Deniz explicitly asks.

Skill and agent sources:

- **`superpowers:*`** — process discipline (auto-loaded bootstrap plus on-demand skills).
- **`dotnet-skills:*`** (Aaron Stannard) — opinionated .NET conventions and optional specialist agents.
- **`dotnet-test:*` / `dotnet-diag:*`** (official Microsoft `dotnet-agent-skills`) — test and diagnostic procedures. Optional; install per harness.
- **Native project skills** — `aspire-source-navigation` ships with the repo; `subagent-model-routing` may exist in local OpenCode setup.

Harness setup, capability-to-invocation mapping, marketplace naming/repair, local-only OpenCode model routing, and LSP wiring live in `docs/agents/README.md`. Never invent a skill or agent ID when a mapped capability is absent.

### Native Project Skills

Harnesses may discover project skill files at session start, but discovery is not invocation. Invoke custom skills only when their trigger fires:

- `aspire-source-navigation` (Tier 1) — compatibility-sensitive work that depends on Aspire/AWS/LocalStack upstream source, package version alignment, or source-level API shape.
- `subagent-model-routing` (Tier 3) — OpenCode-only local skill for choosing configured subagents/models; use when present, but do not require it in other harnesses or fresh checkouts.

### Process Skills

Use the relevant Superpowers process capability before creative work, planning, implementation, debugging, verification, or review workflows. The harness-native names are mapped in `docs/agents/README.md`.

### .NET And Package Work

Use the relevant Aaron `dotnet-skills:*` capability for C# code, public type shape, project/MSBuild structure, NuGet package management, DI, options/configuration, serialization, Aspire domain patterns, and Slopwatch quality gates. Resolve exact harness names from `docs/agents/README.md`.

### Testing

Official Microsoft `dotnet-test:*` and `dotnet-diag:*` skills are optional per harness. When installed, invoke them by capability for test running/filtering, test anti-pattern audits, test gap analysis, coverage/CRAP analysis, or diagnostics. Do not require the entire Microsoft skill catalog for routine work.

TUnit filters with `--treenode-filter`; plain `--filter` / `--nologo` silently run **zero** tests (false green). Prefer `dotnet test --project <csproj>` and confirm total > 0.

### Aspire Routing

| Trigger | Preferred route |
| --- | --- |
| Compatibility-sensitive package work under `src/` or `tests/` that depends on Aspire, AWS integration, or LocalStack.Client upstream internals | `aspire-source-navigation` plus relevant .NET skill |
| Ordinary C# changes under `src/` or `tests/` that do not depend on upstream Aspire/AWS/LocalStack internals | Relevant .NET skill only; do not invoke `aspire-source-navigation` |
| Integration tests under `tests/` | Use `dotnet-skills:aspire-integration-testing` for `DistributedApplicationTestingBuilder` patterns; add `aspire-source-navigation` only for source compatibility or upstream API-shape questions |
| Package version compatibility in `Directory.Packages.props` | `aspire-source-navigation` plus `dotnet-skills:package-management` |
| App-only explicit configuration, `WithEnvironment`, or service environment variable wiring | `dotnet-skills:aspire-configuration` |
| Package/runtime fallback binding, `AddLocalStack`, `UseLocalStack`, `.WithReference(localstack)`, endpoint flow, or LocalStack.Client behavior | `dotnet-skills:aspire-configuration` plus `aspire-source-navigation` |
| Playground ServiceDefaults or observability defaults | `dotnet-skills:aspire-service-defaults` |
| AppHost start/stop/wait/logs/dashboard/deployment workflows | Official Microsoft `aspire`, `aspire-orchestration`, `aspire-monitoring`, or `aspire-deployment` if available |
| New Aspire skeleton creation | Official Microsoft `aspire-init` if available; normally out of scope here |
| AppHost wiring/scaffold/resource graph work | Official Microsoft `aspireify` if available; verify against this repo's package versions |

### Specialist Agents

- Harnesses may keep work in the parent model, use native subagents, or choose repo-local specialists when that improves isolation, cost, or quality; ask Deniz when routing is ambiguous or materially changes risk/cost.
- Use the .NET concurrency specialist capability for racy tests, deadlocks, or async timing bugs.
- Use the .NET performance analyst capability only when measured performance data exists.
- Use the .NET benchmark designer capability when designing or reviewing BenchmarkDotNet/custom benchmark work.
- Use the Microsoft test-generation capability to generate/analyze/improve a test suite end-to-end, when installed. In some harnesses this is a skill named like an agent, not a native subagent.
- Use `explore` for broad codebase discovery across many files when the harness provides it; otherwise use local search tools.
- Use `general` for bounded multi-step research tasks when the harness provides it; otherwise keep the research in the main session.

### Out Of Scope Unless Explicitly Needed

- Akka.NET skills and agents.
- Email/MJML/Mailpit skills.
- EF Core/database performance skills unless a real persistence layer is added.
- Playwright skills unless browser UI tests are added.
- Marketplace publishing skills.
- `dotnet-test:writing-mstest-tests` (this repo uses TUnit, not MSTest) and `dotnet-diag` mobile-crash symbolication skills.

## Semantic Code Navigation

When Rider MCP tools are available, prefer semantic tools for C# symbol questions:

- Declarations and symbol meaning: `search_symbol`, `get_symbol_info`
- File analysis: `get_file_problems`
- Solution/project shape: `get_solution_projects`, `get_project_dependencies`
- Renames and type moves: semantic refactoring tools when approval allows mutation

When Rider is not running, use the current harness's headless LSP for the same symbol questions before falling back to text search. Per-harness LSP wiring and known issues live in `docs/agents/README.md`.

Use text search for docs, manifests, comments, literal strings, and when no semantic tooling is available.

Agent-facing known notes live in `docs/agents/KNOWN_ISSUES.md`. Treat them as triage hints, not permission to refactor unrelated code.

## When Deniz Asks For A Review

Use a code-review mindset. Findings come first, ordered by severity, with file and line references when possible. If there are no findings, say so and mention residual risk or testing gaps.

For interactive reviews, offer options for each issue:

- A recommended option with effort, risk, impact, and maintenance burden.
- One or two alternatives, including doing nothing when reasonable.
- Ask Deniz to confirm before large changes.
