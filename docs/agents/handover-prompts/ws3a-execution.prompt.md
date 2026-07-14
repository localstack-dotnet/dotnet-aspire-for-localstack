---
name: "WS3A execution"
description: "Priming prompt for the next agent entering dotnet-aspire-for-localstack after WS3A planning completed locally on 2026-07-14. The implementation plan is execution-ready but uncommitted; recommended next step is sequential subagent-driven execution of WS3A only."
argument-hint: "Optional execution constraint or reason to pause before Task 1"
agent: "agent"
model: "openai/gpt-5.6-sol"
---

You are the engineer picking up WS3A implementation in `dotnet-aspire-for-localstack`. Planning and compatibility research are complete, but no WS3A production code has been changed. Execute only after the repository approval gate and workspace-isolation preflight are satisfied.

## First Principle

Treat every claim here as current as of 2026-07-14 and verify it against the live repository, git state, `Directory.Packages.props`, and canonical docs before acting. For Aspire/AWS/LocalStack compatibility claims, use the pinned local `external/` checkouts rather than memory or upstream default branches.

## What Just Happened

WS3 was split into two workstreams:

- **WS3A** is planned and implementation-ready: package-owned AppHost configuration, compatibility-preserving deprecation of Client-owned hosting interfaces, internal runtime translation, a best-effort native-endpoint conflict warning, migration docs, and verification.
- **WS3B** is not planned for implementation: target-aware AWS helper attachment and app-like/container resource coverage require research before design. Do not alter those behaviors during WS3A.

The WS3A plan was hardened during a document sweep:

- `IResourceWithEndpoints` and package-managed native endpoint configuration are not planned.
- Issue #12's required manual scenario already works through `localStack.Resource.ConnectionStringExpression`.
- Task 5, the read-only runtime conflict warning, remains in scope.
- Task 6 keeps the signing/proxy limitation aligned in `README.md` Known Limitations, `docs/agents/KNOWN_ISSUES.md`, and a draft `13.4.1` changelog entry.
- Deprecation attributes moved to Task 4, after internal and ordinary repository call sites migrate, so warnings-as-errors does not break an intermediate task checkpoint.
- Task 7 now runs both restore and build.

No production implementation, build, or test run was performed during this planning session.

## Current State You Should Assume Until Verified

- **HEAD:** `86877d4e73620c43d01aac40f04515fdc398ecd7` (`docs: update copyright year to 2025-2026`).
- **Branch/workspace:** `master`, normal checkout rather than a linked worktree, with intended uncommitted documentation plus unrelated `session-ses_0b4e.md`.
- **Active plan:** `docs/plans/2026-07-14-ws3a-apphost-api-boundary.md`.
- **Roadmap:** WS3A is `📐 Planned`; WS3B is `🔜` and explicitly research-first.
- **Pinned packages:** Aspire.Hosting `13.4.6`, Aspire.Hosting.AWS `13.3.1`, LocalStack.Client `2.0.0`, AWSSDK.Core `4.0.9.6`.
- **Pinned upstream refs:** Aspire `v13.4.6` at `87fe259e4fc244c599019a7b1304c85a1488f248`; AWS integration `release_2026-06-19` at `b8f5c040b2a41a655ab9ae4d2c528803d5066ae6`; LocalStack.Client `v2.0.0` at `873efbfbdfb43abe89df2b2e6458dbf90c0a6a92`.
- **Local upstream source:** matching checkouts exist under `external/aspire/`, `external/aws-integrations/`, and `external/localstack-dotnet-client/`.
- **Local spike artifacts:** `/tmp/opencode/ws3-addlocalstack-overloads` and `/tmp/opencode/ws3-terminal-environment-warning` may still exist; verify before relying on them.
- **Tests:** not run in the planning session because changes were documentation-only.

The formal plan and supporting research files are currently untracked. `git diff --name-only` does not show untracked files, so inspect `git status --short` before creating an isolated workspace or preparing any patch.

## Suggested Skills

Invoke these before acting where their triggers apply:

1. `subagent-driven-development` for sequential Task 1-7 execution and per-task independent review.
2. `using-git-worktrees` before implementation because the current checkout is dirty `master` and is not isolated.
3. `test-driven-development` inside each implementation task.
4. `api-design` for the additive public-interface and deprecation work.
5. `aspire-source-navigation` for Aspire/AWS/LocalStack compatibility-sensitive implementation and review.
6. `ms-dotnet-test-run-tests` for TUnit/Microsoft.Testing.Platform commands and non-zero discovery checks.
7. `dotnet-slopwatch` after code/test modifications.
8. `verification-before-completion` before any completion claim.
9. `requesting-code-review` for the final whole-change review.

Follow `.opencode/skills/subagent-model-routing/SKILL.md` when selecting implementer and reviewer agents. Use a different model lineage for review than implementation.

## Workspace Preflight

Do not begin production edits directly on `master` without explicit consent.

1. Read the live status and verify the files listed below still exist.
2. Ask Deniz whether to create an isolated worktree or explicitly work in the current checkout.
3. If creating a worktree, preserve the uncommitted/untracked planning artifacts. Do not stash, reset, clean, or discard the current workspace. Either obtain approval for a planning-doc commit first or deliberately reproduce only the intended planning files in the isolated workspace.
4. Run the clean baseline unit tests in the selected workspace before Task 1. If baseline tests fail, stop and report the exact failures.
5. Change WS3A from `📐 Planned` to `🔨 In progress` when production implementation actually starts. Leave WS3B unchanged.

No commit, push, PR, package release, or publish command is approved by this handoff. Before any commit, present the required summary and proposed Conventional Commit message and obtain explicit approval.

## Mandatory Grounding

Read in this order:

1. `AGENTS.md`.
2. `docs/ROADMAP.md`, especially WS3A and WS3B.
3. `docs/plans/2026-07-14-ws3a-apphost-api-boundary.md`.
4. `docs/plans/ws3-api-boundary-decision-scratchpad.md`.
5. `docs/plans/ws3-addlocalstack-overload-compatibility-spike.md`.
6. `docs/plans/ws3-terminal-environment-warning-spike.md`.
7. `docs/plans/ws3-use-localstack-resource-selector-research.md`.
8. `docs/plans/aws-sdk-signing-region-investigation.md`.
9. `README.md`, `docs/CONFIGURATION.md`, `CHANGELOG.md`, and `docs/agents/KNOWN_ISSUES.md`.
10. The source and test files named by the current task brief.

## Locked WS3A Decisions

- Keep `LocalStack.Client` as an internal runtime dependency.
- Own the recommended AppHost configuration through `LocalStackHostingOptions` and `Aspire:Hosting:LocalStack`.
- Preserve `LocalStack__*` workload values and real-AWS fallback behavior.
- Preserve all released 13.4.0 metadata through 13.x; use non-error CS0618 compatibility adapters and remove only in a future major version.
- Keep `AwsService` as the deliberate public dependency exception.
- Preserve explicit nullable `UseLocalStack(localStack)`; do not add argumentless discovery.
- Do not use `OverloadResolutionPriorityAttribute` or a custom analyzer.
- Do not add `IResourceWithEndpoints`, a native endpoint mode, automatic `AWS_ENDPOINT_URL*` emission, endpoint precedence management, or an AWSSDK.Core signing workaround.
- Keep Task 5: warn when this package observes LocalStack.Client proxy enablement together with native endpoint variables. Warning detection is best-effort, resource-scoped, and read-only.
- Do not change target-aware helper attachment or app-like/container auto-wiring; those are WS3B.

## Execution Notes

- Use `docs/plans/2026-07-14-ws3a-apphost-api-boundary.md` as the single task specification. Generate one task brief at a time; do not dispatch parallel implementers against the shared workspace.
- Task 3 introduces package-owned overloads and immutable resource state but intentionally defers `[Obsolete]` attributes.
- Task 4 migrates runtime and ordinary repository usage before applying obsolete attributes. Dedicated compatibility tests may use narrowly scoped CS0618 pragmas; project-wide suppression is forbidden.
- Existing named `awsConfig:` or `configureContainer:` calls that omit `name` bind the released overload and receive CS0618. Repository call sites must migrate to an exact package-owned shape.
- Target-framework test runs do not prove different SDK compiler behavior. Compare the implemented signatures exactly against the already-verified overload spike; do not misreport net8/net9/net10 runs as SDK 8/9/10 compiler runs.
- TUnit uses Microsoft.Testing.Platform. Confirm every run discovers more than zero tests; do not use VSTest `--filter` or `--nologo`.
- Integration tests require Docker and LocalStack-compatible runtime conditions. Record an exact prerequisite blocker rather than claiming success if unavailable.
- Run Slopwatch exactly as specified in Task 7 after LLM-authored code or test changes.

## Acceptance Criteria

WS3A is complete only when:

- All seven tasks pass their task-scoped spec and quality reviews.
- The final whole-change review is clean or all Important/Critical findings are fixed and re-reviewed.
- Restore, build, unit tests, available integration tests, Slopwatch, and public-surface inspection have current evidence.
- Task 5's warning is proven read-only and its documented ordering limitation is accurate.
- `README.md`, `docs/agents/KNOWN_ISSUES.md`, and draft `13.4.1` changelog language agree.
- WS3A is marked `✅ Done`; WS3B remains research-first.
- No commit, push, or PR occurs without separate explicit approval.

## Final Steering Note

The recommended next action is WS3A Task 1 through the subagent-driven workflow after workspace isolation and a green baseline. Do not expand into WS3B while implementing WS3A, even if adjacent auto-wiring code looks tempting.
