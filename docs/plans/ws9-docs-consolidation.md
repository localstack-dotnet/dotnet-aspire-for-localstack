# WS9 — Docs & Internal-Docs Consolidation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task (inline execution fits this small docs task; subagent-driven is overkill). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the last live items from the gitignored `internal-docs/` into `docs/ROADMAP.md`, give raw notes a new home, then delete `internal-docs/`.

**Architecture:** `internal-docs/` is gitignored (absent from git history). Its content was reviewed and is historical/shipped except two small eager-loading nuggets and the future-capture habit of `todo.md`. We capture those in the roadmap, add an Inbox section to replace `todo.md`, verify nothing live is lost, then delete the folder.

**Tech Stack:** Markdown docs only. No code or tests change.

---

## File Structure

- Modify: `docs/ROADMAP.md` — add two backlog bullets, an Inbox section, and update the WS9 status row.
- Delete: `internal-docs/` (entire folder; untracked/gitignored, so deletion does not appear in `git status`).

No other files change. The two eager-loading nuggets are recorded as roadmap backlog items only — the code/playground changes themselves are deferred to WS6/WS8.

---

### Task 1: Capture remaining items and add the Inbox to the roadmap

**Files:**
- Modify: `docs/ROADMAP.md`

- [ ] **Step 1: Add the eager-loading error-message nugget to WS6**

In the `### WS6 — Refactoring / API quality · P2` section, after the existing line:

```markdown
- Suppress the noisy client-side `"Failed to connect to AWS using AWS SDK config..."` warning (todo #15).
```

add:

```markdown
- Fix the misleading eager-service error message in `LocalStackResourceBuilderExtensions.cs` — it reports a service "is not supported by LocalStack" when the real cause is a missing CLI-name mapping in `LocalStack.Client` (from the PR #8 review).
```

- [ ] **Step 2: Add the eager-loading playground nugget to WS8**

In the `### WS8 — Observability / UX features · P3` section, after the `#26` bullet, add:

```markdown
- Optional: a dedicated `playground/eager-loading/` example AppHost — eager loading is currently exercised only in integration tests, not shown as a runnable sample (from the PR #8 review).
```

- [ ] **Step 3: Add the Inbox section at the end of the document**

Append to the end of `docs/ROADMAP.md`:

```markdown
## Inbox / Untriaged

Drop raw, unsorted ideas here as they come up, then triage them into a workstream (and remove them from this list) during roadmap grooming. This replaces the retired `internal-docs/todo.md` capture spot.

_(empty)_
```

- [ ] **Step 4: Update the WS9 status row to in-progress and link this plan**

In the `## Status & Plan Mapping` table, change the WS9 row from:

```markdown
| WS9 | Docs & internal-docs consolidation | P0 | 🔜 | — |
```

to:

```markdown
| WS9 | Docs & internal-docs consolidation | P0 | 🔨 | [ws9-docs-consolidation.md](plans/ws9-docs-consolidation.md) |
```

- [ ] **Step 5: Verify the edits**

Run: `grep -n "missing CLI-name mapping\|playground/eager-loading\|## Inbox / Untriaged\|ws9-docs-consolidation.md" docs/ROADMAP.md`
Expected: four matching lines.

- [ ] **Step 6: Commit**

```bash
git add docs/ROADMAP.md
git commit -m "docs: capture eager-loading backlog items and add roadmap inbox"
```

---

### Task 2: Safety check — confirm `todo.md` content is fully captured

**Files:**
- Read only: `internal-docs/todo.md`, `docs/ROADMAP.md`

- [ ] **Step 1: Count todo.md items**

Run: `grep -c "^- " internal-docs/todo.md`
Expected: `16`

- [ ] **Step 2: Confirm each todo.md item has a row in the roadmap triage table**

Open `docs/ROADMAP.md` → `## todo.md Triage`. Confirm all 16 items (1–16) are present with a status and workstream mapping. This is the gate before deletion: if any item is missing, stop and add it before continuing.

Expected: all 16 todo.md lines are represented. No commit (verification only).

---

### Task 3: Delete `internal-docs/`

**Files:**
- Delete: `internal-docs/`

- [ ] **Step 1: Delete the folder**

`internal-docs/` is gitignored, so use a plain filesystem delete (not `git rm`):

Run: `rm -rf internal-docs/`

- [ ] **Step 2: Verify it is gone and the working tree is clean**

Run: `ls internal-docs 2>/dev/null && echo "STILL EXISTS" || echo "deleted"`
Expected: `deleted`

Run: `git status --porcelain`
Expected: empty (the folder was untracked, so its removal produces no git change).

---

### Task 4: Mark WS9 done

**Files:**
- Modify: `docs/ROADMAP.md`

- [ ] **Step 1: Flip the WS9 status to done**

In the `## Status & Plan Mapping` table, change the WS9 status cell from `🔨` to `✅`:

```markdown
| WS9 | Docs & internal-docs consolidation | P0 | ✅ | [ws9-docs-consolidation.md](plans/ws9-docs-consolidation.md) |
```

- [ ] **Step 2: Commit**

```bash
git add docs/ROADMAP.md
git commit -m "docs: retire internal-docs, mark roadmap WS9 done"
```

---

## Notes

- The doc-drift fixes from `KNOWN_ISSUES.md` (CONFIGURATION image tag `4.10.0` vs code `4.12.0`; PR-template wording) are **not** in WS9's scope here — they are tracked under WS4/WS9 in the roadmap and handled separately to keep this plan focused.
- Nothing in `internal-docs/` is in git history; once deleted it is unrecoverable. Task 2 is the safety gate.
