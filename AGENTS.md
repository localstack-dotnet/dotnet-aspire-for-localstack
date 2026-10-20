# Agent Instructions

Operating rules for agents working in this repository.

## Purpose

This repository builds `LocalStack.Aspire.Hosting`, a NuGet package in the `Aspire.Hosting.LocalStack` namespace. It extends .NET Aspire hosting so AppHost projects can run AWS-oriented resources against LocalStack for local development and testing.

Compatibility is this project's core behavior, in four directions: Aspire hosting APIs, official `Aspire.Hosting.AWS`, AWS SDK endpoint conventions, and LocalStack.Client-compatible environment variables. A change that compiles and passes tests but breaks one of those is still a defect.

## Autonomy And Approval

Work toward the goal of the request and carry it through to verified completion. Make the code, test, build, CI, and documentation changes the goal needs without stopping to ask, and call out anything beyond the obvious scope in your summary. Answer a question or review request without editing unless it asks for the fix. Diagnose a bug before changing production code for it.

Ask first only before destructive or outward-facing actions. A request that names the action is the approval. When you reach one, finish the rest of the work first and ask once at the end. A plan, handoff, or tool's presence is not that approval.

- Publishing a package or release, running a release or deploy command, or any command against real AWS rather than LocalStack.
- Pushing or opening a PR.
- Amending or rewriting Git history, discarding uncommitted changes you did not make, or deleting files unrelated to the task.
- Deleting, skipping, or weakening a test, or changing what an existing test asserts, unless the request asked for the behavior change that test covers.
- Loosening a rule in this section.

Commit only when the request asks for it; otherwise end with a concise change summary and a proposed Conventional Commit message. Types: `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci`, `chore`. No AI attribution trailers.

## Working Agreements

- Challenge decisions; do not yes-person your way into bad architecture.
- Prefer small correct changes over broad refactors.
- Deniz writes Turkish and English interchangeably; reply in kind.
- When source and docs disagree about runtime behavior, source and tests win.
- On a review, findings come first, ordered by severity, with file and line. When an issue has more than one reasonable fix, recommend one with its effort, risk, and impact, and name an alternative only when it is close. With no findings, say so and name the residual risk.
- Heavyweight process — brainstorming, multi-step planning, TDD branch loops, subagent orchestration — runs when Deniz asks for it. A harness that injects a process bootstrap is offering a tool, not setting policy; this file and Deniz's current request outrank it.
- Keep secrets, tokens, personal machine paths, and local harness configuration out of commits.

## Task Routing

| Task | Read first |
| --- | --- |
| Upstream Aspire, `Aspire.Hosting.AWS`, or LocalStack.Client behavior; package-version alignment; any compatibility conclusion | `.agents/skills/aspire-source-navigation/SKILL.md`. Resolve versions from `Directory.Packages.props` and verify the pinned ref; a default branch is not evidence. Narrow by design — a task merely mentioning Aspire does not qualify. |
| Why a piece of the wiring is shaped the way it is, before changing it | `docs/architecture/hosting-model.md` |
| Running, filtering, or debugging tests | Quality Gates below |
| Backlog, workstream status, open questions | `docs/ROADMAP.md` |
| A failure that smells like an upstream defect | `docs/upstream-watch.md` for upstream defects and closing triggers; `docs/ROADMAP.md` for package defects and test gaps. Neither is licence to refactor nearby code. |
| Consumer-facing configuration behavior | `docs/CONFIGURATION.md` |
| A C# symbol's declaration or meaning | Rider MCP semantic tools while Rider is running, then the harness's headless LSP. Text search is for docs, manifests, comments, and literal strings. |

## Sources Of Truth

Read change-prone facts from the file that owns them instead of from a copy:

- SDK and test runner: `global.json`
- Target frameworks, analyzers, warnings-as-errors, Central Package Management: `Directory.Build.props`
- Package versions: `Directory.Packages.props`
- Package metadata and project references: the project files under `src/`, `tests/`, `playground/`
- Backlog and progress: `docs/ROADMAP.md`
- Runtime behavior: source and tests

Layout: `src/` package source, `tests/` unit and integration tests, `playground/` sample AppHosts and workloads, `docs/` project documentation, `docs/agents/` agent-facing notes.

## Quality Gates

- Use the standard .NET restore, build, and test flow for the files you changed. A documentation-only change needs neither, unless it adds or changes a command that should be proven.
- Tests are TUnit on Microsoft.Testing.Platform. `--filter` and `--nologo` are **false-green**: they are not valid options here and exit with "Zero tests ran". Run `dotnet test --project <csproj>`, narrow with `-- --treenode-filter "/*/*/<ClassName>/*"`, and confirm the reported total is above zero.
- Integration tests need Docker and a LocalStack-compatible runtime.
- The Aspire MCP server only sees AppHosts launched with `aspire start` from the workspace. In-process `DistributedApplicationTestingBuilder` AppHosts are invisible to it, so debugging those stays on logs and the debugger.
- Package versions live in `Directory.Packages.props`; strict analyzers and warnings-as-errors come from the shared project configuration.
- After LLM-authored code, project, or test changes, run Slopwatch when it is available: `slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"`.

## Documentation

- Update docs when behavior, topology, package support, or agent expectations change.
- Give a fact one home and point at it from anywhere else that needs it.
- Prefer the existing natural home over a new document.
- Date a research document.
- Keep `docs/ROADMAP.md` current as the backlog and progress source, and link detailed workstream plans from `docs/plans/` when they land.
- Code comments and XML docs stand on their own, without reaching for a spec, plan, phase, or file path.

## Authority

`AGENTS.md` is the canonical, harness-independent contract. `CLAUDE.md` and the native skill relay under `.claude/` are adapters that point here; they hold no policy of their own. The canonical body of this repository's one project skill lives at `.agents/skills/aspire-source-navigation/SKILL.md`.

Harness-specific details — which skills a given harness exposes, what they are called, how its LSP is wired — belong to the harness, not to this repository. Route by task, as above, and let the harness resolve its own tooling.
