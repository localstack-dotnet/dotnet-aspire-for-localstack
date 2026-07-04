---
name: "Harness setup: official Aspire skills + Aspire MCP server"
description: "Task prompt for a non-Claude harness (Copilot CLI, OpenCode) to install the official Microsoft Aspire skills and register the Aspire MCP server locally, then record its harness-native names in docs/agents/README.md. Claude Code setup was completed 2026-07-04."
argument-hint: "Optional: restrict to skills-only or MCP-only, or name the harness explicitly"
agent: "agent"
---

You are working in the `dotnet-aspire-for-localstack` repository. Claude Code already has the official Microsoft Aspire skills and the Aspire MCP server installed (see `docs/agents/README.md`, Tier 2 section "Official Aspire skills and Aspire MCP server (playground run/debug)"). Your job: do the equivalent setup for **this harness**, then record your harness-native names in the capability mapping.

## Rules before acting

- Read `AGENTS.md` and `docs/agents/README.md` first. This prompt authorizes exactly three things: installing the official Aspire skills for this harness, registering the Aspire MCP server in this harness's **local** configuration, and updating this harness's cells in the one `docs/agents/README.md` table described below. Nothing else — no other policy, routing, or doc changes.
- Never commit installed skill files, MCP configuration, or generated agent config. If installation drops files into the repo tree and they are not already gitignored, add them to `.git/info/exclude` (local-only); do not edit `.gitignore` for this.

## Steps

1. **Verify the Aspire CLI.** `aspire --version` must be 13.3 or newer (`aspire agent` subcommands do not exist before 13.3; this machine was updated to 13.4.6 on 2026-07-04). If older, update via the official install script first and re-verify.
2. **Install the skills** with the Aspire CLI, using the location this harness actually reads:
   - Copilot CLI / GitHub Agent Skills: `aspire agent init --non-interactive --skills all --skill-locations github` (use `standard` instead if this harness reads the standard skills location — resolve from the harness docs, do not guess).
   - OpenCode: `aspire agent init --non-interactive --skills all --skill-locations opencode`, then **restart OpenCode** — skills load at session start. Verify the generated frontmatter is OpenCode-compatible and strip any fields OpenCode rejects (see the frontmatter warning in `docs/agents/README.md`).
   - Expected roster: `aspire` (router/guardrails), `aspire-init`, `aspireify`, `aspire-orchestration`, `aspire-monitoring`, `aspire-deployment`.
3. **Register the Aspire MCP server** — stdio, command `aspire agent mcp` — in this harness's local, non-committed MCP configuration (OpenCode: local `opencode.jsonc`; Copilot CLI: its local MCP config).
4. **Verify.** List skills in the running harness and confirm the Aspire skills resolve by their native IDs. Confirm the MCP server starts; note that it only shows resources when an `aspire start`-launched AppHost is running in this workspace — an empty resource list without one is expected, not an error.
5. **Record the names.** In `docs/agents/README.md`, Tier 2 section "Official Aspire skills and Aspire MCP server (playground run/debug)", replace the "Not installed"/"Not configured" cells in **your harness's column only** with the verified harness-native skill IDs and MCP status. Do not touch other columns or any policy text.
6. **Respect the usage limits** already documented in that section: `aspire`/`aspire-orchestration`/`aspire-monitoring` are for running and debugging playground AppHosts (`aspire start`, never `dotnet run` on an AppHost); `aspire-init`/`aspireify` are not for this repo (AppHosts already exist); `aspire-deployment` is approval-gated and targets real AWS; none of this replaces `aspire-source-navigation` for upstream source-compatibility work.

## Report back

Installed skill IDs as this harness resolves them, where the MCP server was registered, every file created with its git-ignore status, and the exact `docs/agents/README.md` cells you changed.
