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
- `CLAUDE.md` stays relay-only and points to `AGENTS.md`.
- `.github/copilot-instructions.md` stays relay-only and points to `AGENTS.md`.
- Harness-specific skill files are adapters, not policy sources.
- Canonical project skill content lives in `docs/agents/skills/aspire-source-navigation.md`.
- This repo maintains native adapters under `.claude/skills/`, `.opencode/skills/`, and `.github/skills/`.
- Do not create `.vscode` skill folders. GitHub Copilot Agent Skills also support `.agents/skills/`, but this repo intentionally does not maintain that extra adapter location.
- Never commit secrets, OAuth tokens, personal machine paths, or local MCP config.

OpenCode loads project skills when the session starts. After changing `.opencode/skills/**`, tell the user to restart OpenCode if they need the new skill active in the running UI.

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

## Skills Used In This Project

Skills encode workflow discipline and retrieval-led reasoning. Use exact installed skill names when invoking skills.

### Session-Start Preload

Load these project/harness skills explicitly at session start (alongside `using-superpowers`, which the harness auto-loads). All other skills below are **on-demand**: invoke them only when their trigger fires.

- `aspire-source-navigation` — compatibility/source work in `src/` and `tests/`.
- `subagent-model-routing` — OpenCode only; subagent dispatch and model routing.

### Process Skills (on-demand)

| Skill | Trigger |
| --- | --- |
| `brainstorming` | Feature, behavior, design, or refactor work before implementation |
| `writing-plans` | Multi-step implementation planning after design approval |
| `test-driven-development` | Production bugfixes and feature implementation |
| `systematic-debugging` | Bugs, test failures, unexpected behavior, flaky behavior |
| `verification-before-completion` | Before claiming work is complete or passing |
| `requesting-code-review` / `receiving-code-review` | Review workflows and review feedback |

### .NET And Package Work

| Skill | Trigger |
| --- | --- |
| `dotnet-project-structure` | Solution, project, target framework, SDK, or shared MSBuild changes |
| `package-management` | NuGet package additions, removals, or version changes |
| `modern-csharp-coding-standards` | C# production or test code changes |
| `type-design-performance` | Public type shape, collections, allocation-sensitive choices |
| `csharp-concurrency-patterns` | Async, synchronization, delays, channels, task scheduling |
| `dependency-injection-patterns` | DI registrations or service composition |
| `microsoft-extensions-configuration` | Options/configuration binding or validation |
| `serialization` | JSON or other serialization contract changes |
| `dotnet-slopwatch` | After LLM-authored code, project, or test changes when available |

### Aspire Routing

| Trigger | Preferred route |
| --- | --- |
| Core package work under `src/` | `aspire-source-navigation` plus relevant .NET skill |
| Integration tests under `tests/` | `aspire-source-navigation`; add `aspire-integration-testing` for `DistributedApplicationTestingBuilder` patterns and adapt examples to this repo's test framework |
| Package version compatibility in `Directory.Packages.props` | `aspire-source-navigation` plus `package-management` |
| Explicit AppHost-to-service configuration, `WithEnvironment`, `LocalStack__*`, fallback behavior | `aspire-configuration` plus `aspire-source-navigation` |
| Playground ServiceDefaults or observability defaults | `aspire-service-defaults` |
| AppHost start/stop/wait/logs/dashboard/deployment workflows | Official Microsoft `aspire`, `aspire-orchestration`, `aspire-monitoring`, or `aspire-deployment` if available |
| New Aspire skeleton creation | Official Microsoft `aspire-init` if available; normally out of scope here |
| AppHost wiring/scaffold/resource graph work | Official Microsoft `aspireify` if available; verify against this repo's package versions |

### Specialist Agents

- Harnesses may keep work in the parent model, use native subagents, or choose repo-local specialists when that improves isolation, cost, or quality; ask Deniz when routing is ambiguous or materially changes risk/cost.
- Use `dotnet-concurrency-specialist` for racy tests, deadlocks, or async timing bugs.
- Use `dotnet-performance-analyst` only when measured performance data exists.
- Use `explore` for broad codebase discovery across many files when the harness provides it; otherwise use local search tools.
- Use `general` for bounded multi-step research tasks when the harness provides it; otherwise keep the research in the main session.

### Out Of Scope Unless Explicitly Needed

- Akka.NET skills and agents.
- Email/MJML/Mailpit skills.
- EF Core/database performance skills unless a real persistence layer is added.
- Playwright skills unless browser UI tests are added.
- Marketplace publishing skills.

## Semantic Code Navigation

When Rider MCP tools are available, prefer semantic tools for C# symbol questions:

- Declarations and symbol meaning: `search_symbol`, `get_symbol_info`
- File analysis: `get_file_problems`
- Solution/project shape: `get_solution_projects`, `get_project_dependencies`
- Renames and type moves: semantic refactoring tools when approval allows mutation

Use text search for docs, manifests, comments, literal strings, and when Rider is unavailable.

Agent-facing known notes live in `docs/agents/KNOWN_ISSUES.md`. Treat them as triage hints, not permission to refactor unrelated code.

## When Deniz Asks For A Review

Use a code-review mindset. Findings come first, ordered by severity, with file and line references when possible. If there are no findings, say so and mention residual risk or testing gaps.

For interactive reviews, offer options for each issue:

- A recommended option with effort, risk, impact, and maintenance burden.
- One or two alternatives, including doing nothing when reasonable.
- Ask Deniz to confirm before large changes.
