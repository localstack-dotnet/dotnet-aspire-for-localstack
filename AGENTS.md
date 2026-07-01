# Agent Instructions

Operating rules for LLM/code agents working in this repository.

## Purpose And Scope

This repository builds `LocalStack.Aspire.Hosting`, a NuGet package in the `Aspire.Hosting.LocalStack` namespace.

The package extends .NET Aspire hosting so AppHost projects can run AWS-oriented resources against LocalStack for local development and testing. Compatibility with Aspire hosting APIs, official `Aspire.Hosting.AWS` behavior, AWS SDK endpoint conventions, and LocalStack.Client-compatible environment variables is core project behavior.

`AGENTS.md` is the compact, harness-independent contract. Harness-specific files and skill relays are adapters, not policy sources.

## Operating Style

- Be direct, practical, and clear.
- Challenge decisions when needed; do not yes-person your way into bad architecture.
- Prefer small correct changes over broad refactors.
- Deniz communicates in Turkish and English interchangeably; respond in the language that best matches the current message.
- For non-trivial work, inspect relevant docs and code before acting. Runtime behavior comes from source and tests first, then README/docs.

## Approval Gate

### Do Not Without Explicit Approval

- Start coding a new feature.
- Refactor production code.
- Fix production-code bugs unless Deniz has explicitly asked you to fix, apply, proceed, or equivalent.
- Modify build system behavior.
- Change CI/CD pipelines.
- Change agent policy, approval gates, capability routing, skill triggers, or harness adapter behavior.
- Weaken, skip, delete, or substantially rewrite tests to change what behavior is verified.
- Run deployment, publish, or package-release commands.
- Commit, amend, push, or create a PR.

Approval phrases include `go`, `apply`, `proceed`, `basla`, and `yap`.

### Allowed Without Additional Approval

- Documentation-only edits that do not change agent policy, approval gates, capability routing, skill triggers, or harness adapter behavior.
- Broken internal link fixes.
- Minor comment improvements that do not change behavior.
- Read-only discovery commands and dry-run checks.
- Bug diagnosis and test-failure investigation before proposing or applying a production-code fix.

### Before Any Commit

Present a concise change summary and proposed Conventional Commit message, then ask for approval.

Commit messages must use `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, or `chore`. Do not add AI attribution trailers.

## First Decision Flow

- Read-only question: inspect the relevant docs/code and answer directly.
- Documentation-only edit: allowed when it does not alter agent policy, approval gates, capability routing, skill triggers, or harness adapter behavior; update the natural existing doc rather than creating a new one by default.
- Bug report or failing test: diagnose first with systematic debugging and relevant domain capability; require approval before production-code edits unless already requested.
- Test-code changes: approval follows the behavior under change; weakening or removing coverage always requires explicit approval.
- New feature, refactor, build change, CI change, package/version change, release task, or public API change: require explicit approval before mutation.
- Agent-instruction or skill-routing docs: treat changes as policy/infrastructure edits; require approval unless the task explicitly asks for them.
- Aspire/AWS/LocalStack source compatibility: use Aspire source navigation plus the relevant .NET capability before acting.
- Test execution or filtering: use the test-running capability when present; for TUnit, avoid false-green filters and confirm the run executed more than zero tests.
- Review request: use a code-review mindset; findings come first, ordered by severity, with file/line references when possible.

## Project Sources Of Truth

Read change-prone facts from their source files instead of copying them here:

- SDK and test runner: `global.json`
- Target frameworks, analyzers, warnings-as-errors, and Central Package Management: `Directory.Build.props`
- Package versions: `Directory.Packages.props`
- Package metadata and project references: project files under `src/`, `tests/`, and `playground/`
- Runtime behavior: source code and tests first, then README/docs
- Backlog and progress: `docs/ROADMAP.md`

Repository layout:

- `src/`: package source
- `tests/`: unit and integration tests
- `playground/`: sample AppHosts and workloads
- `docs/`: project documentation
- `docs/agents/`: harness adapter guide, capability mapping, and known agent notes

## Documentation Hygiene

- Update docs when behavior, topology, package support, or harness expectations change.
- Prefer consolidation over new docs when an existing doc is the natural home.
- Research docs must include a date.
- Keep `docs/ROADMAP.md` current as the backlog and progress source. Link detailed workstream plans from `docs/plans/` when they land.
- Code comments and XML docs must be self-contained; do not reference specs, plans, phases, or external file paths from code comments.

## Harness Independence

- `AGENTS.md` is the canonical repository contract.
- Harness-specific instructions and skill files are adapters, not policy sources.
- Harness-native invocation names, LSP wiring, local-only setup notes, and skill maintenance live in `docs/agents/README.md`.
- Never commit secrets, OAuth tokens, personal machine paths, local MCP config, or personal OpenCode model-routing config.

## Quality Notes

- Use the standard .NET restore/build/test flow appropriate to the files changed.
- Integration tests require Docker and LocalStack-compatible runtime conditions.
- Package versions live in `Directory.Packages.props`; do not hand-edit package versions into individual project files.
- Strict analyzers and warnings-as-errors are enabled through shared project configuration.
- Documentation-only changes do not require build/test unless they add or change commands that should be validated.
- TUnit filters with `--treenode-filter`; plain `--filter` / `--nologo` silently run zero tests. Prefer `dotnet test --project <csproj>` and confirm total > 0.
- If Slopwatch is available after LLM-authored code, project, or test changes, run `slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"`.

## Aspire Source Compatibility

Use `aspire-source-navigation` before changing or reviewing work that depends on Aspire hosting internals, `Aspire.Hosting.AWS`, LocalStack.Client behavior, package version alignment, source-level API shape, `AddLocalStack`, `UseLocalStack`, `.WithReference(localstack)`, endpoint/configuration flow, manifest behavior, CloudFormation/CDK, Lambda, or AWS SDK wiring.

For read-only explanation questions, inspect this repository's docs/code first. Invoke `aspire-source-navigation` only when the answer depends on upstream internals, version-specific API shape, or a compatibility conclusion.

Do not invoke it for ordinary Markdown edits, general C# cleanup, or playground-only work that does not depend on Aspire/AWS/LocalStack internals.

The skill must resolve package versions from `Directory.Packages.props`, then cross-check those versions against local upstream checkouts under `external/`. Do not silently use upstream default branches for compatibility-sensitive checks.

Preferred local checkout layout:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/localstack-dotnet-client/{ref}/
```

The `external/` tree is local-only and ignored by git. GitHub MCP is allowed for tag/ref discovery, release verification, or fallback reads when local source is unavailable; it is not the default source-reading path.

## Capability Routing

Use capabilities, not memorized harness names. Resolve the harness-native invocation from `docs/agents/README.md` before invoking a skill or specialist agent.

If a bootstrap or process skill is already injected by the harness, follow it immediately; use `docs/agents/README.md` to map additional capabilities, not to delay the active process workflow.

Capability tiers:

- **Tier 0**: process discipline; follow when injected by the harness.
- **Tier 1**: required when triggered for this repo's Aspire/LocalStack package work and available in the harness.
- **Tier 2**: optional by judgment; use when it materially improves correctness, safety, test quality, or diagnostics.
- **Tier 3**: local-only convenience; use when present, never assume fresh checkouts have it.
- **Out of scope**: do not use unless this repo adds that technology or Deniz explicitly asks.

Required or common capabilities:

- Process skills before creative work, planning, implementation, debugging, verification, or review workflows.
- Relevant .NET skills for C# code, public API shape, project/MSBuild structure, NuGet packages, DI, options/configuration, serialization, Aspire patterns, and Slopwatch quality gates.
- `aspire-source-navigation` for compatibility-sensitive Aspire/AWS/LocalStack source checks.
- Test skills for running/filtering tests, test anti-pattern audits, gap analysis, coverage/CRAP analysis, or diagnostics when installed and relevant.
- Specialist agents for concurrency, performance, benchmarks, broad exploration, or bounded research when the harness exposes them and the task benefits from isolation.

### Critical Aspire Routing

| Trigger | Preferred route |
| --- | --- |
| Compatibility-sensitive package work under `src/` or `tests/` that depends on Aspire, AWS integration, or LocalStack.Client upstream internals | `aspire-source-navigation` plus relevant .NET skill |
| Ordinary C# changes under `src/` or `tests/` that do not depend on upstream Aspire/AWS/LocalStack internals | Relevant .NET skill only |
| Integration tests under `tests/` | Aspire integration-testing capability; add `aspire-source-navigation` only for source compatibility or upstream API-shape questions |
| Package version compatibility in `Directory.Packages.props` | `aspire-source-navigation` plus package-management capability |
| App-only explicit configuration, `WithEnvironment`, or service environment variable wiring | Aspire configuration capability |
| Package/runtime fallback binding, `AddLocalStack`, `UseLocalStack`, `.WithReference(localstack)`, endpoint flow, or LocalStack.Client behavior | Aspire configuration capability plus `aspire-source-navigation` |
| Playground ServiceDefaults or observability defaults | Aspire ServiceDefaults capability |
| AppHost start/stop/wait/logs/dashboard/deployment workflows | Official Aspire orchestration/monitoring/deployment capability when available; deployment remains approval-gated |

Out of scope unless explicitly needed: Akka.NET, email/MJML/Mailpit, EF Core/database performance, Playwright, marketplace publishing, MSTest-specific skills, and mobile-crash symbolication.

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
