# Agent Harness Guide

Date: 2026-07-01

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

`AGENTS.md` routes by capability so the contract stays harness-neutral. Resolve each capability to the current harness's native invocation before acting:

Tier meanings:

- **Tier 0** — bootstrap/process discipline; follow when injected by the harness.
- **Tier 1** — required when triggered for this repo's Aspire/LocalStack package work; invoke before acting when installed or shipped.
- **Tier 2** — optional by judgment; use when it materially improves correctness, safety, test quality, or diagnostics.
- **Tier 3** — local-only convenience; use when present, never assume fresh checkouts have it.
- **Out of scope** — do not use unless this repo adds that technology or Deniz explicitly asks.

| Capability | Kind | Tier | Claude Code / Copilot CLI | OpenCode |
| --- | --- | --- | --- | --- |
| Superpowers process discipline | Process skill | Tier 0 | `superpowers:<name>` via skill invocation | `<name>` via `skill` |
| Aspire source compatibility | Native project skill | Tier 1; shipped in repo | `aspire-source-navigation` via skill invocation | `aspire-source-navigation` via `skill` |
| OpenCode local model routing | Local project skill | Tier 3; local-only when present | Not applicable | `subagent-model-routing` via `skill` when present |
| .NET project structure | Domain skill | Tier 1 when installed | `dotnet-skills:project-structure` via skill invocation | `dotnet-project-structure` via `skill` |
| NuGet package management | Domain skill | Tier 1 when installed | `dotnet-skills:package-management` via skill invocation | `package-management` via `skill` |
| C# coding standards | Domain skill | Tier 1 when installed | `dotnet-skills:csharp-coding-standards` via skill invocation | `modern-csharp-coding-standards` via `skill` |
| C# type design/performance | Domain skill | Tier 1 when installed | `dotnet-skills:csharp-type-design-performance` via skill invocation | `type-design-performance` via `skill` |
| C# concurrency patterns | Domain skill | Tier 1 when installed | `dotnet-skills:csharp-concurrency-patterns` via skill invocation | `csharp-concurrency-patterns` via `skill` |
| Dependency injection patterns | Domain skill | Tier 1 when installed | `dotnet-skills:microsoft-extensions-dependency-injection` via skill invocation | `dependency-injection-patterns` via `skill` |
| Options/configuration patterns | Domain skill | Tier 1 when installed | `dotnet-skills:microsoft-extensions-configuration` via skill invocation | `microsoft-extensions-configuration` via `skill` |
| Serialization contracts | Domain skill | Tier 1 when installed | `dotnet-skills:serialization` via skill invocation | `serialization` via `skill` |
| Slopwatch quality gate | Quality skill | Tier 1 when available after LLM-authored code/project/test changes | `dotnet-skills:slopwatch` via skill invocation | `dotnet-slopwatch` via `skill` |
| Aspire explicit configuration | Domain skill | Tier 1 when installed | `dotnet-skills:aspire-configuration` via skill invocation | `aspire-configuration` via `skill` |
| Aspire ServiceDefaults | Domain skill | Tier 1 when installed | `dotnet-skills:aspire-service-defaults` via skill invocation | `aspire-service-defaults` via `skill` |
| Aspire integration testing | Domain skill | Tier 1 when installed | `dotnet-skills:aspire-integration-testing` via skill invocation | `aspire-integration-testing` via `skill` |
| .NET test running/filtering | Procedure skill | Tier 2; use by judgment when installed | `dotnet-test:run-tests` and `dotnet-test:filter-syntax` via skill invocation | `ms-dotnet-test-run-tests` and `ms-dotnet-test-filter-syntax` via `skill` |
| .NET test anti-pattern audit | Procedure skill | Tier 2; use by judgment when installed | `dotnet-test:test-anti-patterns` via skill invocation | `ms-dotnet-test-test-anti-patterns` via `skill` |
| .NET test gap analysis | Procedure skill | Tier 2; use by judgment when installed | `dotnet-test:test-gap-analysis` via skill invocation | `ms-dotnet-test-test-gap-analysis` via `skill` |
| .NET test generation | Procedure skill | Tier 2; use by judgment when installed | `dotnet-test:code-testing-agent` via skill invocation | `ms-dotnet-test-code-testing-agent` via `skill`; despite the name, this is not an OpenCode `subagent_type` |
| .NET concurrency specialist | Specialist agent | Tier 2; use when harness exposes a matching agent | Harness-native agent if installed | `dotnet-concurrency-specialist` via `task` when available |
| .NET performance analyst | Specialist agent | Tier 2; use only with measured performance data | Harness-native agent if installed | `dotnet-performance-analyst` via `task` when available |
| .NET benchmark designer | Specialist agent | Tier 2; use for BenchmarkDotNet/custom benchmark design | Harness-native agent if installed | `dotnet-benchmark-designer` via `task` when available |

OpenCode exposes skill frontmatter names. Official Microsoft skills are installed with `ms-dotnet-*` prefixes to avoid collisions with generic names and Aaron's `dotnet-skills` set. Specialist agents are not skills in OpenCode; dispatch them with `task` only when the harness exposes the matching `subagent_type`.

If a bootstrap or process skill is already active, follow it immediately. Use this adapter guide to map additional capabilities after the active process workflow tells you what to invoke.

`aspire-source-navigation` is a narrow project skill for compatibility-sensitive source checks. Do not invoke it just because a task mentions Aspire; invoke it when the task depends on upstream Aspire/AWS/LocalStack internals, package-version alignment, source-level API shape, or a compatibility conclusion. For read-only explanation questions, inspect this repository's docs/code first and invoke the skill only when upstream version-specific evidence is needed.

For package version updates, use the package-management capability for Central Package Management and `dotnet` command mechanics. Use `aspire-source-navigation` for compatibility evidence and upstream source checks. If the guidance overlaps, package-management governs how packages are edited; source navigation governs whether the version/API behavior is compatible.

Official Microsoft **Aspire** orchestration skills (`aspire`, `aspireify`, `aspire-orchestration`, `aspire-monitoring`, `aspire-deployment`, `aspire-init`) are a separate source, not part of `dotnet-agent-skills`, and may not be installed. Use the harness-native name only if the harness exposes it.

Availability is not activation. Except for the Superpowers bootstrap, skills do not run automatically; invoke the mapped skill or dispatch the mapped specialist agent when the trigger applies. If a mapped capability is not loaded in the current harness, skip optional rows or ask before installing, changing harness configuration, or substituting another tool. Do not invent an ID.

Copilot VS Code differs from Copilot CLI: repository skills under `.github/skills/` are discoverable, but external Superpowers, Aaron `dotnet-skills`, and Microsoft `dotnet-agent-skills` availability depends on the active Copilot/agent environment. Resolve them from the running harness instead of assuming Claude-style plugin names.

### Per-Developer OpenCode Setup

Some OpenCode conveniences are intentionally local-only and ignored by git: `opencode.jsonc`, `.opencode/agents/`, and `.opencode/skills/subagent-model-routing/`. They may define model-routed agents such as `deepseek-light`, `codex-coder`, `glm-hardcore`, `codex-review`, or local slash commands, but they are not shipped project infrastructure.

When present, `subagent-model-routing` can help choose among local OpenCode agents and providers. Do not treat it as a required project skill, and do not assume another checkout exposes the same `subagent_type` names.

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

Resolve `{ref}` from package versions in `Directory.Packages.props` and verified upstream tags/releases. The `external/` tree is ignored by git. Do not commit upstream clones.
