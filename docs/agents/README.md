# Agent Harness Guide

Date: 2026-06-18

This directory contains repository-specific guidance for AI coding agents.

## Source Of Truth

`AGENTS.md` is the canonical always-on contract. Harness-specific files relay to it or expose native discovery points, but they do not own policy.

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

## Skill Maintenance

The canonical skill body lives in `docs/agents/skills/aspire-source-navigation.md`. Native `SKILL.md` files should stay small and point back to the canonical document.

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
