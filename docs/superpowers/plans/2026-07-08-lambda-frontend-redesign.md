# Lambda Frontend (Command Center) Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Command Center UI (`playground/lambda/LocalStack.Lambda.Frontend/wwwroot`) as a "Pipeline Hero" demo screen with a live SVG pipeline, keyed diff-rendering, a detail drawer, and a refined dark theme, plus one backend addition (`AccessCount`).

**Architecture:** Vanilla ES modules under `wwwroot/js/` (no build chain). The client polls `/api/snapshot` every 1.5 s, diffs it against the previous snapshot in a small store, and feeds the diff to three renderers: keyed table/feed reconciliation (no full DOM rebuilds), a fixed-topology SVG pipeline that pulses on changes, and a slide-over drawer for per-link/per-event detail. The backend widens `LinkSummary` with `AccessCount` computed server-side.

**Tech Stack:** ASP.NET Core minimal API (.NET, existing), vanilla JS ES modules, hand-authored SVG + CSS animations. Verification via Aspire CLI/MCP + Playwright MCP + curl.

**Spec:** `docs/superpowers/specs/2026-07-08-lambda-frontend-redesign-design.md`

## Global Constraints

- **NO COMMITS at any point.** Deniz's instruction: everything lands uncommitted; the final task presents a change summary + proposed Conventional Commit message and stops for approval (AGENTS.md approval gate).
- No npm, no build step, no third-party JS/CSS. Everything self-contained under `wwwroot/`.
- The AppHost runs under `aspire start`. NEVER run `dotnet build`/`dotnet run` against these projects while it runs (file locks, MSB3491/CS2012). C# changes → `aspire resource rebuild Frontend` (or Aspire MCP `execute_resource_command` with command `rebuild`). Static `wwwroot` changes → browser reload only, no rebuild.
- Discover the Frontend URL from `aspire ps --format Json` or Aspire MCP `list_resources` (Frontend → `urls`). Do not hardcode `http://localhost:5233` (it changes across runs; examples below use it as a stand-in).
- C# style: match the repo — sealed records, collection expressions `[.. ]`, `ConfigureAwait(false)`, `StringComparer.Ordinal`, strict analyzers + warnings-as-errors are ON.
- JS style: `"use strict"` implied by modules, `const`/arrow functions, explicit and boring over clever (Deniz preference), no frameworks.
- Code comments must be self-contained; never reference the spec/plan files from code.
- Accessibility: keep `aria-*` patterns, focus management (drawer focus + restore), Esc to close, `prefers-reduced-motion` disables animations.
- API only widens: `/api/config`, `/api/links`, `/api/analytics`, `/api/shorten` stay; snapshot keeps `timelineEvents` even though the new UI doesn't render it.
- JSON casing: minimal API serializes camelCase — the new field arrives as `accessCount`.
- Status colors: green = ready/created, amber = pending, purple = accessed, red = errors. Single accent: teal `#5eead4`.

## File Structure

```
playground/lambda/LocalStack.Lambda.Frontend/
├─ Models.cs                     (modify: LinkSummary + AccessCount)
├─ DynamoDbItemMapper.cs         (modify: ToLinkSummary gains accessCount param)
├─ Program.cs                    (modify: unified snapshot loader computing counts)
└─ wwwroot/
   ├─ index.html                 (rewrite: new layout, favicon, module script)
   ├─ styles.css                 (rewrite: tokens + components, appended per task)
   ├─ app.js                     (DELETE — replaced by js/ modules)
   └─ js/
      ├─ api.js                  (fetch wrapper + trace header + endpoints)
      ├─ format.js               (relative/absolute time helpers)
      ├─ state.js                (snapshot store + diff)
      ├─ tables.js               (keyed reconciliation: links table + events feed)
      ├─ drawer.js               (slide-over detail: QR, lifecycle, raw tree)
      ├─ pipeline.js             (SVG topology, counters, pulse animations)
      └─ main.js                 (bootstrap, poll loop, form, popover, visibility)
playground/lambda/README.md      (modify: Command Center section)
```

---

### Task 1: Backend — `AccessCount` on `LinkSummary`

**Files:**
- Modify: `playground/lambda/LocalStack.Lambda.Frontend/Models.cs`
- Modify: `playground/lambda/LocalStack.Lambda.Frontend/DynamoDbItemMapper.cs`
- Modify: `playground/lambda/LocalStack.Lambda.Frontend/Program.cs`

**Interfaces:**
- Consumes: existing `IAmazonDynamoDB`, `DynamoDbItemMapper.ToAnalyticsEventSummary`.
- Produces: `LinkSummary.AccessCount` (int, JSON `accessCount`) consumed by Tasks 3–5; `LoadSnapshotDataAsync` helper used by `/api/snapshot`, `/api/links`, `/api/analytics`.

- [ ] **Step 1: Add `AccessCount` to `LinkSummary`** in `Models.cs` (position it before `RawItem`):

```csharp
internal sealed record LinkSummary(
    string Slug,
    string Url,
    string CreatedAt,
    string QrStatus,
    string? QrObjectKey,
    string? QrGeneratedAt,
    int AccessCount,
    IReadOnlyDictionary<string, object?> RawItem);
```

- [ ] **Step 2: Thread the count through the mapper** — in `DynamoDbItemMapper.cs` change `ToLinkSummary` to:

```csharp
public static LinkSummary ToLinkSummary(Dictionary<string, AttributeValue> item, int accessCount) => new(
    item["Slug"].S,
    item["Url"].S,
    item["CreatedAt"].S,
    item.TryGetValue("QrStatus", out var status) ? status.S : "Pending",
    item.TryGetValue("QrObjectKey", out var key) ? key.S : null,
    item.TryGetValue("QrGeneratedAt", out var generatedAt) ? generatedAt.S : null,
    accessCount,
    ToRawItem(item));
```

- [ ] **Step 3: Replace the two loaders in `Program.cs` with one snapshot loader.** Delete `LoadLinksAsync` and `LoadAnalyticsEventsAsync` and add (same position, after `app.RunAsync()`):

```csharp
async Task<(List<LinkSummary> Links, List<AnalyticsEventSummary> AnalyticsEvents)> LoadSnapshotDataAsync(
    IAmazonDynamoDB dynamoDb,
    CancellationToken cancellationToken)
{
    var urlsScan = await dynamoDb.ScanAsync(new ScanRequest { TableName = urlsTableName }, cancellationToken).ConfigureAwait(false);
    var analyticsScan = await dynamoDb.ScanAsync(new ScanRequest { TableName = analyticsTableName }, cancellationToken).ConfigureAwait(false);

    List<AnalyticsEventSummary> allAnalyticsEvents = [.. analyticsScan.Items.Select(DynamoDbItemMapper.ToAnalyticsEventSummary)];

    // Counted over the full scan, before the feed cap, so counts stay correct once the feed saturates.
    var accessCounts = allAnalyticsEvents
        .Where(analyticsEvent => string.Equals(analyticsEvent.EventType, "url_accessed", StringComparison.Ordinal))
        .GroupBy(analyticsEvent => analyticsEvent.Slug, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    List<LinkSummary> links = [.. urlsScan.Items
        .Select(item => DynamoDbItemMapper.ToLinkSummary(item, accessCounts.GetValueOrDefault(item["Slug"].S)))
        .OrderByDescending(link => link.CreatedAt, StringComparer.Ordinal)
        .Take(maxFeedItems)];

    List<AnalyticsEventSummary> analyticsEvents = [.. allAnalyticsEvents
        .OrderByDescending(analyticsEvent => analyticsEvent.Timestamp, StringComparer.Ordinal)
        .Take(maxFeedItems)];

    return (links, analyticsEvents);
}
```

- [ ] **Step 4: Rewire the three GET endpoints** to the unified loader (bodies only change; telemetry scopes stay):

```csharp
app.MapGet("/api/snapshot", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var (links, analyticsEvents) = await LoadSnapshotDataAsync(dynamoDb, cancellationToken).ConfigureAwait(false);
    var timelineEvents = BuildTimelineEvents(links, analyticsEvents);

    return Results.Ok(new CommandCenterSnapshot(links, analyticsEvents, timelineEvents));
});

app.MapGet("/api/links", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var (links, _) = await LoadSnapshotDataAsync(dynamoDb, cancellationToken).ConfigureAwait(false);

    return Results.Ok(links);
});

app.MapGet("/api/analytics", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var (_, analyticsEvents) = await LoadSnapshotDataAsync(dynamoDb, cancellationToken).ConfigureAwait(false);

    return Results.Ok(analyticsEvents);
});
```

`BuildTimelineEvents` stays unchanged (snapshot shape only widens).

- [ ] **Step 5: Rebuild the running resource and verify.**

Run: `aspire resource rebuild Frontend --non-interactive` (from `playground/lambda/LocalStack.Lambda.AppHost`)
Expected: rebuild succeeds; resource returns to Running. A build error fails this step — fix before proceeding.

- [ ] **Step 6: Verify the field over HTTP** (Frontend URL from `aspire ps`; create a link first if the tables are empty):

```bash
curl -s -X POST http://localhost:5233/api/shorten -H "Content-Type: application/json" -d '{"Url":"https://example.com/plan-check"}'
curl -s http://localhost:5233/api/snapshot | python -c "import json,sys; s=json.load(sys.stdin); print(s['links'][0]['accessCount'], s['links'][0]['slug'])"
```

Expected: prints `0 <slug>`. Then `curl -s -o /dev/null http://<apigw-http-url>/<slug>` (API Gateway http URL from `aspire ps`), wait ~3 s for the analyzer, re-run the snapshot check → prints `1 <slug>`.

---

### Task 2: Static skeleton — new `index.html`, base `styles.css`, core modules, working shorten form

**Files:**
- Rewrite: `playground/lambda/LocalStack.Lambda.Frontend/wwwroot/index.html`
- Rewrite: `playground/lambda/LocalStack.Lambda.Frontend/wwwroot/styles.css`
- Delete: `playground/lambda/LocalStack.Lambda.Frontend/wwwroot/app.js`
- Create: `wwwroot/js/api.js`, `wwwroot/js/format.js`, `wwwroot/js/main.js`

**Interfaces:**
- Produces for later tasks:
  - `api.js`: `setTraceRefreshRequests(bool)`, `fetchJson(url, options)`, `loadConfig()`, `loadSnapshot()`, `shorten(url)`.
  - `format.js`: `relativeTime(isoString, nowMs?) → string`, `absoluteTime(isoString) → string`, `EMPTY_VALUE`.
  - `main.js` context object `{ shortUrl(slug), qrUrl(slug), copy(text, label), openLink(slug), openEvent(eventId) }` — Tasks 3–4 consume exactly these names.
  - DOM ids consumed later: `pipeline-root`, `pipeline-empty`, `pipeline-offline`, `links-body`, `links-count`, `links-empty`, `events-feed`, `events-count`, `events-empty`, `drawer`, `drawer-backdrop`, `drawer-title`, `drawer-content`, `drawer-close`, `settings-popover`, `settings-toggle`, `trace-refresh-toggle`, `refresh-now`, `live-dot`, `live-status`, `shorten-form`, `url-input`, `shorten-error`.
- After this task the page loads clean, polls, shows live status, and the shorten form works. Tables/pipeline/drawer render in Tasks 3–5.

- [ ] **Step 1: Write the new `index.html`** (complete file):

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>LocalStack Command Center</title>
  <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='7' fill='%230a1120'/%3E%3Cpath d='M18 4 7 19h7l-2 9L23 13h-7z' fill='%235eead4'/%3E%3C/svg%3E" />
  <link rel="stylesheet" href="styles.css" />
</head>
<body>
  <header class="app-header">
    <h1 class="app-title">URL Shortener <span class="app-title-badge">Command Center</span></h1>

    <div class="header-status" role="status" aria-live="polite">
      <span id="live-dot" class="live-dot" aria-hidden="true"></span>
      <span id="live-status">connecting…</span>
    </div>

    <div class="header-tools">
      <button type="button" id="settings-toggle" class="icon-button" aria-haspopup="true"
              aria-expanded="false" aria-controls="settings-popover" title="Settings">
        ⚙<span class="visually-hidden">Settings</span>
      </button>
      <div id="settings-popover" class="popover" hidden>
        <label class="switch">
          <input type="checkbox" id="trace-refresh-toggle" />
          <span class="switch-track" aria-hidden="true"></span>
          <span>
            <strong>Trace refresh requests</strong>
            <small>Off keeps table polling out of Aspire traces.</small>
          </span>
        </label>
        <button type="button" id="refresh-now" class="secondary-button">Refresh now</button>
      </div>
    </div>
  </header>

  <section class="shorten-bar" aria-label="Create a short URL">
    <form id="shorten-form" class="shorten-form">
      <input type="url" id="url-input" name="url" placeholder="https://example.com" aria-label="URL to shorten" required />
      <button type="submit" class="primary-button">Shorten</button>
    </form>
    <p id="shorten-error" class="form-error" role="alert" hidden></p>
  </section>

  <main class="layout">
    <section class="panel pipeline-panel" aria-label="Live pipeline">
      <div id="pipeline-root" class="pipeline-root"></div>
      <p id="pipeline-empty" class="pipeline-empty" hidden>Shorten your first URL to watch it flow through the pipeline.</p>
      <p id="pipeline-offline" class="pipeline-offline" hidden>Connection lost — retrying…</p>
    </section>

    <div class="data-grid">
      <section class="panel" aria-labelledby="links-heading">
        <div class="panel-heading">
          <h2 id="links-heading">Links</h2>
          <span id="links-count" class="count-pill">0</span>
        </div>
        <div class="table-scroll">
          <table class="links-table">
            <thead>
              <tr>
                <th>Short</th>
                <th>Original URL</th>
                <th>Created</th>
                <th>QR</th>
                <th class="num">Hits</th>
              </tr>
            </thead>
            <tbody id="links-body"></tbody>
          </table>
        </div>
        <p id="links-empty" class="empty-note">No links yet. Shorten one above.</p>
      </section>

      <section class="panel" aria-labelledby="events-heading">
        <div class="panel-heading">
          <h2 id="events-heading">Analytics events</h2>
          <span id="events-count" class="count-pill">0</span>
        </div>
        <ol id="events-feed" class="events-feed"></ol>
        <p id="events-empty" class="empty-note">No analytics events yet.</p>
      </section>
    </div>
  </main>

  <div id="drawer-backdrop" class="backdrop" hidden></div>
  <aside id="drawer" class="drawer" role="dialog" aria-modal="true" aria-labelledby="drawer-title" hidden>
    <header class="drawer-header">
      <h2 id="drawer-title"></h2>
      <button type="button" id="drawer-close" class="icon-button" aria-label="Close details">×</button>
    </header>
    <div id="drawer-content" class="drawer-content"></div>
  </aside>

  <script type="module" src="js/main.js"></script>
</body>
</html>
```

- [ ] **Step 2: Write the base `styles.css`** (complete file at this point; Tasks 4/5/7 append their blocks at the end):

```css
:root {
  color-scheme: dark;
  --bg: #0a1120;
  --surface: #101a2c;
  --surface-raised: #16233a;
  --border: #23324a;
  --text: #e2ebf8;
  --muted: #8fa1b8;
  --accent: #5eead4;
  --accent-dim: rgba(94, 234, 212, 0.14);
  --ok: #34d399;
  --pending: #fbbf24;
  --danger: #f87171;
  --accessed: #a78bfa;
  --radius: 10px;
  --radius-sm: 6px;
  --font-mono: "Cascadia Mono", "SFMono-Regular", Consolas, monospace;
  --shadow: 0 8px 28px rgba(2, 6, 23, 0.4);
}

* { box-sizing: border-box; }

/* Author display rules (grid/flex) would otherwise override the UA's [hidden] style. */
[hidden] { display: none !important; }

body {
  min-height: 100vh;
  margin: 0;
  font-family: Inter, "Segoe UI", system-ui, -apple-system, sans-serif;
  font-size: 0.875rem;
  background: var(--bg);
  color: var(--text);
}

button, input { font: inherit; }
a { color: var(--accent); }

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  white-space: nowrap;
}

button:focus-visible, a:focus-visible, [tabindex]:focus-visible,
.switch input:focus-visible + .switch-track {
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}

/* ---------- Header ---------- */

.app-header {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px 24px;
  border-bottom: 1px solid var(--border);
}

.app-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  letter-spacing: -0.02em;
}

.app-title-badge {
  margin-left: 8px;
  padding: 2px 8px;
  border: 1px solid var(--border);
  border-radius: 999px;
  color: var(--muted);
  font-size: 0.7rem;
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  vertical-align: middle;
}

.header-status {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
  min-width: 12rem;
  justify-content: flex-end;
  color: var(--muted);
  font-size: 0.8rem;
  font-variant-numeric: tabular-nums;
}

.live-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ok);
}

.live-dot.is-error { background: var(--danger); }

.header-tools { position: relative; }

.icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--text);
  cursor: pointer;
}

.popover {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  z-index: 20;
  display: grid;
  gap: 12px;
  width: 20rem;
  padding: 14px;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface-raised);
  box-shadow: var(--shadow);
}

.switch {
  display: grid;
  grid-template-columns: auto 1fr;
  align-items: center;
  gap: 10px;
  cursor: pointer;
}

.switch input { position: absolute; opacity: 0; }
.switch strong { display: block; font-size: 0.82rem; }
.switch small { display: block; margin-top: 2px; color: var(--muted); font-size: 0.72rem; line-height: 1.3; }

.switch-track {
  position: relative;
  width: 36px;
  height: 20px;
  border-radius: 999px;
  background: #334155;
}

.switch-track::after {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: #cbd5e1;
  content: "";
  transition: transform 140ms ease, background 140ms ease;
}

.switch input:checked + .switch-track { background: var(--accent-dim); }
.switch input:checked + .switch-track::after { transform: translateX(16px); background: var(--accent); }

/* ---------- Shorten bar ---------- */

.shorten-bar { padding: 16px 24px 0; }

.shorten-form { display: flex; gap: 8px; max-width: 44rem; }

.shorten-form input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  outline: none;
  background: var(--surface);
  color: var(--text);
}

.shorten-form input:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-dim); }

.primary-button {
  padding: 10px 18px;
  border: 0;
  border-radius: var(--radius-sm);
  background: var(--accent);
  color: #04252b;
  font-weight: 700;
  cursor: pointer;
}

.primary-button:hover { filter: brightness(1.08); }

.secondary-button {
  padding: 7px 12px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--surface);
  color: var(--text);
  font-weight: 600;
  cursor: pointer;
  text-decoration: none;
}

.form-error { max-width: 44rem; margin: 8px 0 0; color: var(--danger); font-size: 0.82rem; }

/* ---------- Layout & panels ---------- */

.layout { display: grid; gap: 16px; padding: 16px 24px 24px; }

.panel {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
}

.panel-heading {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border);
}

.panel-heading h2 {
  margin: 0;
  color: var(--muted);
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.count-pill {
  padding: 1px 8px;
  border: 1px solid var(--border);
  border-radius: 999px;
  color: var(--muted);
  font-size: 0.72rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.data-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: 16px;
  align-items: start;
}

.empty-note { margin: 0; padding: 20px 16px; color: var(--muted); text-align: center; }

/* ---------- Links table ---------- */

.table-scroll { overflow-x: auto; }

.links-table { width: 100%; border-collapse: collapse; }

.links-table th, .links-table td {
  padding: 9px 12px;
  border-top: 1px solid var(--border);
  text-align: left;
  vertical-align: middle;
  white-space: nowrap;
}

.links-table thead th { border-top: 0; color: var(--muted); font-size: 0.68rem; letter-spacing: 0.08em; text-transform: uppercase; }
.links-table .num { text-align: right; font-variant-numeric: tabular-nums; }
.links-table td.cell-url { max-width: 0; width: 60%; overflow: hidden; text-overflow: ellipsis; }

.data-row { cursor: pointer; }
.data-row:hover td, .data-row:focus-visible td { background: rgba(148, 163, 184, 0.06); }
.data-row:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }

.short-cell { display: inline-flex; align-items: center; gap: 6px; }
.short-cell a { font-family: var(--font-mono); font-size: 0.82rem; text-decoration: none; }
.short-cell a:hover { text-decoration: underline; }

.copy-button {
  padding: 2px 6px;
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  opacity: 0;
}

.data-row:hover .copy-button, .copy-button:focus-visible { opacity: 1; }
.copy-button:hover { border-color: var(--border); color: var(--text); }

.qr-thumb {
  display: block;
  width: 30px;
  height: 30px;
  padding: 2px;
  border-radius: 4px;
  background: #fff;
}

.badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 0.66rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.badge::before { width: 6px; height: 6px; border-radius: 50%; content: ""; }
.badge-pending { background: rgba(251, 191, 36, 0.14); color: var(--pending); }
.badge-pending::before { border: 2px solid var(--pending); border-top-color: transparent; background: none; animation: spin 0.8s linear infinite; }
.badge-created { background: rgba(52, 211, 153, 0.14); color: var(--ok); }
.badge-created::before { background: var(--ok); }
.badge-accessed { background: rgba(167, 139, 250, 0.14); color: var(--accessed); }
.badge-accessed::before { background: var(--accessed); }
.badge-muted { background: rgba(148, 163, 184, 0.14); color: var(--muted); }
.badge-muted::before { background: var(--muted); }

@keyframes spin { to { transform: rotate(360deg); } }

.row-enter { animation: row-enter 0.9s ease; }

@keyframes row-enter {
  from { background: var(--accent-dim); }
  to { background: transparent; }
}

/* ---------- Events feed ---------- */

.events-feed { display: grid; gap: 0; margin: 0; padding: 0; max-height: 30rem; overflow-y: auto; list-style: none; }

.event-item {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 10px;
  padding: 9px 16px;
  border-top: 1px solid var(--border);
  cursor: pointer;
}

.event-item:first-child { border-top: 0; }
.event-item:hover, .event-item:focus-visible { background: rgba(148, 163, 184, 0.06); }
.event-item .slug { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-family: var(--font-mono); font-size: 0.8rem; }
.event-item time { color: var(--muted); font-size: 0.74rem; white-space: nowrap; font-variant-numeric: tabular-nums; }
```

- [ ] **Step 3: Delete `wwwroot/app.js`.**

- [ ] **Step 4: Create `wwwroot/js/api.js`:**

```js
let traceRefreshRequests = false;

export function setTraceRefreshRequests(enabled) {
  traceRefreshRequests = enabled;
}

export async function fetchJson(url, options = {}) {
  const headers = new Headers(options.headers ?? {});
  headers.set("X-Command-Center-Trace-Refresh", String(traceRefreshRequests));

  const response = await fetch(url, { ...options, headers });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`${url} failed (${response.status}): ${text}`);
  }

  return response.json();
}

export function loadConfig() {
  return fetchJson("/api/config");
}

export function loadSnapshot() {
  return fetchJson("/api/snapshot");
}

export function shorten(url) {
  return fetchJson("/api/shorten", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ Url: url }),
  });
}
```

- [ ] **Step 5: Create `wwwroot/js/format.js`:**

```js
export const EMPTY_VALUE = "–";

export function relativeTime(isoString, nowMs = Date.now()) {
  if (!isoString) {
    return EMPTY_VALUE;
  }

  const time = Date.parse(isoString);
  if (Number.isNaN(time)) {
    return isoString;
  }

  const seconds = Math.round((nowMs - time) / 1000);
  if (seconds < 5) {
    return "just now";
  }
  if (seconds < 60) {
    return `${seconds}s ago`;
  }

  const minutes = Math.round(seconds / 60);
  if (minutes < 60) {
    return `${minutes}m ago`;
  }

  const hours = Math.round(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }

  return new Date(time).toLocaleString();
}

export function absoluteTime(isoString) {
  if (!isoString) {
    return EMPTY_VALUE;
  }

  const time = Date.parse(isoString);
  return Number.isNaN(time) ? isoString : new Date(time).toLocaleString();
}
```

- [ ] **Step 6: Create `wwwroot/js/main.js`** — bootstrap version for this task. The four commented hook lines are filled in by Tasks 3–6; write the file exactly like this now:

```js
import { loadConfig, loadSnapshot, shorten } from "./api.js";

const POLL_INTERVAL_MS = 1500;

const liveDot = document.getElementById("live-dot");
const liveStatus = document.getElementById("live-status");
const shortenForm = document.getElementById("shorten-form");
const urlInput = document.getElementById("url-input");
const shortenError = document.getElementById("shorten-error");

const state = {
  apiGatewayBaseUrl: "",
  refreshing: false,
  latest: null,
};

export const context = {
  shortUrl: (slug) => `${state.apiGatewayBaseUrl}/${slug}`,
  qrUrl: (slug) => `${state.apiGatewayBaseUrl}/${slug}/qr`,
  copy: copyText,
  openLink: () => {},
  openEvent: () => {},
};

async function copyText(text, label) {
  try {
    await navigator.clipboard.writeText(text);
    liveStatus.textContent = `${label} copied`;
  } catch {
    liveStatus.textContent = `${label} copy blocked by browser permissions`;
  }
}

function setOnline(updatedAt) {
  liveDot.classList.remove("is-error");
  liveStatus.textContent = `updated ${updatedAt.toLocaleTimeString()}`;
}

function setOffline() {
  liveDot.classList.add("is-error");
  liveStatus.textContent = "reconnecting…";
}

async function refresh() {
  if (state.refreshing) {
    return;
  }

  state.refreshing = true;
  try {
    const snapshot = await loadSnapshot();
    state.latest = snapshot;
    // Task 3 wires: store diff + renderLinks/renderEvents here.
    // Task 4 wires: drawer sync here.
    // Task 5 wires: pipeline update here.
    setOnline(new Date());
  } catch {
    setOffline();
    // Task 6 wires: pipeline offline band here.
  } finally {
    state.refreshing = false;
  }
}

async function handleShorten(submitEvent) {
  submitEvent.preventDefault();
  shortenError.hidden = true;

  const url = urlInput.value.trim();
  if (!url) {
    return;
  }

  try {
    await shorten(url);
    urlInput.value = "";
    await refresh();
  } catch (error) {
    shortenError.textContent = `Failed to shorten URL: ${error.message}`;
    shortenError.hidden = false;
  }
}

async function init() {
  shortenForm.addEventListener("submit", handleShorten);

  const config = await loadConfig();
  state.apiGatewayBaseUrl = String(config.apiGatewayBaseUrl ?? "").replace(/\/+$/, "");

  await refresh();
  setInterval(refresh, POLL_INTERVAL_MS);
}

init().catch((error) => {
  shortenError.textContent = `Failed to initialize command center: ${error.message}`;
  shortenError.hidden = false;
  setOffline();
});
```

- [ ] **Step 7: Verify in the browser** (no rebuild needed — static files):

1. Playwright `browser_navigate` to the Frontend URL, `browser_console_messages` → **zero errors** (favicon 404 must be gone).
2. Header shows green dot + "updated HH:MM:SS" changing every ~1.5 s without layout shift.
3. Type a URL into the form, submit → input clears, no error shown; `curl .../api/links` shows the new link.
4. Screenshot at 1512×945: slim single-row header, shorten bar, empty pipeline panel, two empty panels with "No links yet…" notes.

---

### Task 3: Store diff + keyed tables/feed rendering

**Files:**
- Create: `wwwroot/js/state.js`
- Create: `wwwroot/js/tables.js`
- Modify: `wwwroot/js/main.js` (wire store + renderers into `refresh`)

**Interfaces:**
- Consumes: `format.js`, `main.js` context (`shortUrl`, `qrUrl`, `copy`, `openLink`, `openEvent`).
- Produces:
  - `state.js`: `createStore()` → `{ applySnapshot(snapshot) → { links, events, changes, isFirstSnapshot } }` where `changes = { newLinkSlugs, qrReadySlugs, createdEventSlugs, accessedEventSlugs }` (all string arrays). Task 5 consumes `changes` exactly by these names.
  - `tables.js`: `renderLinks(links, context)`, `renderEvents(events, context)`, `reconcileChildren(container, items, keyOf, create, update)`.

- [ ] **Step 1: Create `wwwroot/js/state.js`:**

```js
export function createStore() {
  let previousLinks = null;
  let previousEventIds = null;

  function applySnapshot(snapshot) {
    const links = snapshot.links ?? [];
    const events = snapshot.analyticsEvents ?? [];
    const isFirstSnapshot = previousLinks === null;

    const changes = {
      newLinkSlugs: [],
      qrReadySlugs: [],
      createdEventSlugs: [],
      accessedEventSlugs: [],
    };

    if (!isFirstSnapshot) {
      for (const link of links) {
        const before = previousLinks.get(link.slug);
        if (!before) {
          changes.newLinkSlugs.push(link.slug);
          if (link.qrStatus === "Ready") {
            changes.qrReadySlugs.push(link.slug);
          }
        } else if (before.qrStatus !== "Ready" && link.qrStatus === "Ready") {
          changes.qrReadySlugs.push(link.slug);
        }
      }

      for (const event of events) {
        if (previousEventIds.has(event.eventId)) {
          continue;
        }
        if (event.eventType === "url_accessed") {
          changes.accessedEventSlugs.push(event.slug);
        } else {
          changes.createdEventSlugs.push(event.slug);
        }
      }
    }

    previousLinks = new Map(links.map((link) => [link.slug, link]));
    previousEventIds = new Set(events.map((event) => event.eventId));

    return { links, events, changes, isFirstSnapshot };
  }

  return { applySnapshot };
}
```

- [ ] **Step 2: Create `wwwroot/js/tables.js`:**

```js
import { relativeTime, absoluteTime, EMPTY_VALUE } from "./format.js";

const linksBody = document.getElementById("links-body");
const linksCount = document.getElementById("links-count");
const linksEmpty = document.getElementById("links-empty");
const eventsFeed = document.getElementById("events-feed");
const eventsCount = document.getElementById("events-count");
const eventsEmpty = document.getElementById("events-empty");

export function reconcileChildren(container, items, keyOf, create, update) {
  const existing = new Map();
  for (const child of [...container.children]) {
    existing.set(child.dataset.key, child);
  }

  let cursor = null;
  for (const item of items) {
    const key = keyOf(item);
    let element = existing.get(key);

    if (element) {
      update(element, item);
      existing.delete(key);
    } else {
      element = create(item);
      element.dataset.key = key;
      element.classList.add("row-enter");
    }

    const anchor = cursor ? cursor.nextSibling : container.firstChild;
    if (element !== anchor) {
      container.insertBefore(element, anchor);
    }
    cursor = element;
  }

  for (const leftover of existing.values()) {
    leftover.remove();
  }
}

function isQrReady(link) {
  return link.qrStatus === "Ready" && Boolean(link.qrObjectKey);
}

function attachRowActivation(element, activate) {
  element.tabIndex = 0;
  element.addEventListener("click", (event) => {
    if (!event.target.closest("a, button")) {
      activate();
    }
  });
  element.addEventListener("keydown", (event) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      activate();
    }
  });
}

function renderQrCell(cell, link, context) {
  cell.dataset.qrStatus = link.qrStatus;
  cell.replaceChildren();

  if (isQrReady(link)) {
    const img = document.createElement("img");
    img.className = "qr-thumb";
    img.alt = `QR code for /${link.slug}`;
    img.src = context.qrUrl(link.slug);
    img.addEventListener("error", () => {
      const badge = document.createElement("span");
      badge.className = "badge badge-muted";
      badge.textContent = "QR unavailable";
      img.replaceWith(badge);
    });
    cell.appendChild(img);
  } else {
    const badge = document.createElement("span");
    badge.className = "badge badge-pending";
    badge.textContent = "Pending";
    cell.appendChild(badge);
  }
}

function createLinkRow(link, context) {
  const row = document.createElement("tr");
  row.className = "data-row";
  row.setAttribute("aria-label", `Details for /${link.slug}`);
  attachRowActivation(row, () => context.openLink(link.slug));

  const shortCell = document.createElement("td");
  const shortWrap = document.createElement("span");
  shortWrap.className = "short-cell";
  const anchor = document.createElement("a");
  anchor.href = context.shortUrl(link.slug);
  anchor.target = "_blank";
  anchor.rel = "noopener noreferrer";
  anchor.textContent = `/${link.slug}`;
  const copyButton = document.createElement("button");
  copyButton.type = "button";
  copyButton.className = "copy-button";
  copyButton.title = "Copy short URL";
  copyButton.textContent = "⧉";
  copyButton.addEventListener("click", () => context.copy(context.shortUrl(link.slug), "Short URL"));
  shortWrap.append(anchor, copyButton);
  shortCell.appendChild(shortWrap);

  const urlCell = document.createElement("td");
  urlCell.className = "cell-url";

  const createdCell = document.createElement("td");
  const createdTime = document.createElement("time");
  createdCell.appendChild(createdTime);

  const qrCell = document.createElement("td");
  qrCell.className = "cell-qr";

  const hitsCell = document.createElement("td");
  hitsCell.className = "num";

  row.append(shortCell, urlCell, createdCell, qrCell, hitsCell);
  updateLinkRow(row, link, context);
  return row;
}

function updateLinkRow(row, link, context) {
  const [, urlCell, createdCell, qrCell, hitsCell] = row.children;

  if (urlCell.textContent !== link.url) {
    urlCell.textContent = link.url;
    urlCell.title = link.url;
  }

  const createdTime = createdCell.firstChild;
  createdTime.textContent = relativeTime(link.createdAt);
  createdTime.title = absoluteTime(link.createdAt);

  if (qrCell.dataset.qrStatus !== link.qrStatus) {
    renderQrCell(qrCell, link, context);
  }

  const hits = String(link.accessCount ?? 0);
  if (hitsCell.textContent !== hits) {
    hitsCell.textContent = hits;
  }
}

export function renderLinks(links, context) {
  linksCount.textContent = String(links.length);
  linksEmpty.hidden = links.length > 0;

  reconcileChildren(
    linksBody,
    links,
    (link) => link.slug,
    (link) => createLinkRow(link, context),
    (row, link) => updateLinkRow(row, link, context),
  );
}

function badgeModifier(eventType) {
  if (eventType === "url_created") {
    return "created";
  }
  return eventType === "url_accessed" ? "accessed" : "muted";
}

function createEventItem(event, context) {
  const item = document.createElement("li");
  item.className = "event-item";
  item.setAttribute("aria-label", `Details for event ${event.eventId}`);
  attachRowActivation(item, () => context.openEvent(event.eventId));

  const badge = document.createElement("span");
  badge.className = `badge badge-${badgeModifier(event.eventType)}`;
  badge.textContent = String(event.eventType ?? "unknown").replace(/_/g, " ");

  const slug = document.createElement("span");
  slug.className = "slug";
  slug.textContent = event.slug ? `/${event.slug}` : EMPTY_VALUE;
  slug.title = event.originalUrl || "";

  const time = document.createElement("time");

  item.append(badge, slug, time);
  updateEventItem(item, event);
  return item;
}

function updateEventItem(item, event) {
  const time = item.querySelector("time");
  time.textContent = relativeTime(event.timestamp);
  time.title = `${absoluteTime(event.timestamp)} · ${event.ipAddress || "unknown"} · ${event.userAgent || "unknown"}`;
}

export function renderEvents(events, context) {
  eventsCount.textContent = String(events.length);
  eventsEmpty.hidden = events.length > 0;

  reconcileChildren(
    eventsFeed,
    events,
    (event) => event.eventId,
    (event) => createEventItem(event, context),
    (item, event) => updateEventItem(item, event),
  );
}
```

- [ ] **Step 3: Wire into `main.js`.** Add imports at the top:

```js
import { createStore } from "./state.js";
import { renderLinks, renderEvents } from "./tables.js";
```

Add after the `state` declaration:

```js
const store = createStore();
```

Replace the `refresh` try-block body up to `setOnline` with:

```js
    const snapshot = await loadSnapshot();
    state.latest = store.applySnapshot(snapshot);
    renderLinks(state.latest.links, context);
    renderEvents(state.latest.events, context);
    // Task 4 wires: drawer sync here.
    // Task 5 wires: pipeline update here.
    setOnline(new Date());
```

- [ ] **Step 4: Verify rendering and element stability.**

1. Reload the page → links table and events feed populate; counts match; empty notes hidden.
2. **Stability proof:** Playwright strict-mode click on a row must succeed with no retry (this failed against the old UI with "element is not stable"): `browser_click` with target `#links-body tr >> nth=0` → succeeds within one attempt.
3. Element identity across polls: `browser_evaluate` → `(() => { const el = document.querySelector('#links-body tr'); window.__probe = el; return true; })()`, wait ≥3 s (two polls), then `(() => window.__probe === document.querySelector('#links-body tr'))()` → `true`.
4. Create a new link → its row appears at the top with a brief highlight; existing rows do not flicker.
5. `curl` a short URL → within ~3 s the Hits cell increments and a purple "url accessed" event enters the feed.

---

### Task 4: Detail drawer

**Files:**
- Create: `wwwroot/js/drawer.js`
- Modify: `wwwroot/js/main.js` (context.openLink/openEvent + drawer sync + Esc)
- Modify: `wwwroot/styles.css` (append drawer block)

**Interfaces:**
- Consumes: `format.js`; context (`shortUrl`, `qrUrl`, `copy`); DOM ids `drawer`, `drawer-backdrop`, `drawer-title`, `drawer-content`, `drawer-close`.
- Produces: `openLink(slug)`, `openEvent(eventId)`, `close()`, `isOpen()`, `sync(latest, context)` — `main.js` calls `sync` after every snapshot and routes Esc.

- [ ] **Step 1: Create `wwwroot/js/drawer.js`:**

```js
import { absoluteTime, relativeTime, EMPTY_VALUE } from "./format.js";

const drawer = document.getElementById("drawer");
const backdrop = document.getElementById("drawer-backdrop");
const title = document.getElementById("drawer-title");
const content = document.getElementById("drawer-content");
const closeButton = document.getElementById("drawer-close");

const state = {
  mode: null,
  key: null,
  returnFocus: null,
};

export function isOpen() {
  return !drawer.hidden;
}

export function openLink(slug) {
  state.mode = "link";
  state.key = slug;
  show();
}

export function openEvent(eventId) {
  state.mode = "event";
  state.key = eventId;
  show();
}

export function close() {
  if (!isOpen()) {
    return;
  }
  drawer.hidden = true;
  backdrop.hidden = true;
  state.mode = null;
  state.key = null;
  if (state.returnFocus?.isConnected) {
    state.returnFocus.focus();
  }
  state.returnFocus = null;
}

function show() {
  state.returnFocus = document.activeElement;
  drawer.hidden = false;
  backdrop.hidden = false;
  closeButton.focus();
}

export function sync(latest, context) {
  if (!isOpen() || !latest) {
    return;
  }

  if (state.mode === "link") {
    const link = latest.links.find((candidate) => candidate.slug === state.key);
    if (!link) {
      close();
      return;
    }
    renderLink(link, latest.events, context);
  } else {
    const event = latest.events.find((candidate) => candidate.eventId === state.key);
    if (!event) {
      close();
      return;
    }
    renderEvent(event);
  }
}

function metaRow(term, value, isLink = false) {
  const dt = document.createElement("dt");
  dt.textContent = term;
  const dd = document.createElement("dd");
  if (isLink && value) {
    const anchor = document.createElement("a");
    anchor.href = value;
    anchor.target = "_blank";
    anchor.rel = "noopener noreferrer";
    anchor.textContent = value;
    dd.appendChild(anchor);
  } else {
    dd.textContent = value || EMPTY_VALUE;
  }
  return [dt, dd];
}

function actionLink(text, href) {
  const anchor = document.createElement("a");
  anchor.className = "secondary-button";
  anchor.href = href;
  anchor.target = "_blank";
  anchor.rel = "noopener noreferrer";
  anchor.textContent = text;
  return anchor;
}

function actionButton(text, onClick) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "secondary-button";
  button.textContent = text;
  button.addEventListener("click", onClick);
  return button;
}

function lifecycleItem(kind, text, timestamp) {
  const item = document.createElement("li");
  item.className = `lifecycle-item lifecycle-${kind}`;
  const label = document.createElement("span");
  label.textContent = text;
  const time = document.createElement("time");
  time.textContent = relativeTime(timestamp);
  time.title = absoluteTime(timestamp);
  item.append(label, time);
  return item;
}

function rawSection(summaryText, rawItem) {
  const details = document.createElement("details");
  details.className = "raw-detail";
  const summary = document.createElement("summary");
  summary.textContent = summaryText;
  details.appendChild(summary);

  const tree = document.createElement("div");
  tree.className = "raw-tree";
  tree.appendChild(renderRawValue("item", rawItem, 0));
  details.appendChild(tree);
  return details;
}

function renderRawValue(name, value, depth) {
  const container = document.createElement("div");
  container.className = "raw-node";

  if (value && typeof value === "object") {
    const details = document.createElement("details");
    details.open = depth < 2;
    const summary = document.createElement("summary");
    summary.textContent = Array.isArray(value) ? `${name} [${value.length}]` : `${name} {${Object.keys(value).length}}`;
    details.appendChild(summary);

    for (const [childName, childValue] of Object.entries(value)) {
      details.appendChild(renderRawValue(childName, childValue, depth + 1));
    }

    container.appendChild(details);
    return container;
  }

  const leaf = document.createElement("code");
  leaf.textContent = `${name}: ${JSON.stringify(value)}`;
  container.appendChild(leaf);
  return container;
}

function renderLink(link, events, context) {
  title.textContent = `/${link.slug}`;
  content.replaceChildren();

  const qrFigure = document.createElement("div");
  qrFigure.className = "drawer-qr";
  if (link.qrStatus === "Ready" && link.qrObjectKey) {
    const img = document.createElement("img");
    img.alt = `QR code for /${link.slug}`;
    img.src = context.qrUrl(link.slug);
    img.addEventListener("error", () => {
      const badge = document.createElement("span");
      badge.className = "badge badge-muted";
      badge.textContent = "QR unavailable";
      img.replaceWith(badge);
    });
    qrFigure.appendChild(img);
  } else {
    const badge = document.createElement("span");
    badge.className = "badge badge-pending";
    badge.textContent = "QR pending";
    qrFigure.appendChild(badge);
  }
  content.appendChild(qrFigure);

  const actions = document.createElement("div");
  actions.className = "drawer-actions";
  actions.append(
    actionLink("Open short URL", context.shortUrl(link.slug)),
    actionButton("Copy short URL", () => context.copy(context.shortUrl(link.slug), "Short URL")),
    actionLink("Open QR endpoint", context.qrUrl(link.slug)),
    actionButton("Copy QR URL", () => context.copy(context.qrUrl(link.slug), "QR URL")),
  );
  content.appendChild(actions);

  const meta = document.createElement("dl");
  meta.className = "drawer-meta";
  meta.append(
    ...metaRow("Original URL", link.url, true),
    ...metaRow("Created", absoluteTime(link.createdAt)),
    ...metaRow("QR generated", absoluteTime(link.qrGeneratedAt)),
    ...metaRow("QR object key", link.qrObjectKey),
    ...metaRow("Hits", String(link.accessCount ?? 0)),
  );
  content.appendChild(meta);

  const heading = document.createElement("h3");
  heading.textContent = "Lifecycle";
  content.appendChild(heading);

  const lifecycle = document.createElement("ol");
  lifecycle.className = "lifecycle";
  lifecycle.appendChild(lifecycleItem("created", "Link created", link.createdAt));
  if (link.qrGeneratedAt) {
    lifecycle.appendChild(lifecycleItem("ready", "QR generated", link.qrGeneratedAt));
  }
  for (const event of events.filter((candidate) => candidate.slug === link.slug && candidate.eventType === "url_accessed")) {
    lifecycle.appendChild(lifecycleItem("accessed", "Accessed", event.timestamp));
  }
  content.appendChild(lifecycle);

  content.appendChild(rawSection("Raw DynamoDB item (Urls)", link.rawItem));
}

function renderEvent(event) {
  title.textContent = `${String(event.eventType ?? "unknown").replace(/_/g, " ")} /${event.slug}`;
  content.replaceChildren();

  const meta = document.createElement("dl");
  meta.className = "drawer-meta";
  meta.append(
    ...metaRow("Event id", event.eventId),
    ...metaRow("Timestamp", absoluteTime(event.timestamp)),
    ...metaRow("Original URL", event.originalUrl, true),
    ...metaRow("Client IP", event.ipAddress),
    ...metaRow("User agent", event.userAgent),
  );
  content.appendChild(meta);

  content.appendChild(rawSection("Raw DynamoDB item (UrlAnalytics)", event.rawItem));
}

closeButton.addEventListener("click", close);
backdrop.addEventListener("click", close);
```

- [ ] **Step 2: Wire into `main.js`.** Add import:

```js
import * as drawer from "./drawer.js";
```

Set the real context handlers (replace the two no-op lines in `context`):

```js
  openLink: (slug) => {
    drawer.openLink(slug);
    drawer.sync(state.latest, context);
  },
  openEvent: (eventId) => {
    drawer.openEvent(eventId);
    drawer.sync(state.latest, context);
  },
```

Replace the `// Task 4 wires: drawer sync here.` comment with:

```js
    drawer.sync(state.latest, context);
```

Add a document-level Esc handler inside `init()` (before `loadConfig`):

```js
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      drawer.close();
    }
  });
```

- [ ] **Step 3: Append the drawer block to `styles.css`:**

```css
/* ---------- Drawer ---------- */

.backdrop {
  position: fixed;
  inset: 0;
  z-index: 30;
  background: rgba(2, 6, 23, 0.6);
}

.drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  z-index: 31;
  display: flex;
  flex-direction: column;
  width: min(26rem, 92vw);
  border-left: 1px solid var(--border);
  background: var(--surface-raised);
  box-shadow: var(--shadow);
  animation: drawer-in 0.18s ease;
}

@keyframes drawer-in {
  from { transform: translateX(24px); opacity: 0; }
  to { transform: none; opacity: 1; }
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
}

.drawer-header h2 { margin: 0; font-size: 1rem; font-family: var(--font-mono); }

.drawer-content { display: grid; gap: 16px; padding: 16px; overflow-y: auto; }

.drawer-qr { display: flex; justify-content: center; }
.drawer-qr img { width: 11rem; height: 11rem; padding: 8px; border-radius: var(--radius); background: #fff; }

.drawer-actions { display: flex; flex-wrap: wrap; gap: 8px; }

.drawer-meta { display: grid; grid-template-columns: auto 1fr; gap: 6px 14px; margin: 0; font-size: 0.82rem; }
.drawer-meta dt { color: var(--muted); }
.drawer-meta dd { margin: 0; overflow-wrap: anywhere; }

.drawer-content h3 {
  margin: 0;
  color: var(--muted);
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-transform: uppercase;
}

.lifecycle { display: grid; gap: 8px; margin: 0; padding: 0; list-style: none; }

.lifecycle-item {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 10px;
  padding-left: 14px;
  position: relative;
  font-size: 0.82rem;
}

.lifecycle-item::before {
  position: absolute;
  left: 0;
  top: 0.3em;
  width: 7px;
  height: 7px;
  border-radius: 50%;
  content: "";
}

.lifecycle-created::before { background: var(--ok); }
.lifecycle-ready::before { background: var(--accent); }
.lifecycle-accessed::before { background: var(--accessed); }
.lifecycle-item time { color: var(--muted); font-size: 0.74rem; }

.raw-detail { border: 1px solid var(--border); border-radius: var(--radius-sm); background: var(--surface); }
.raw-detail > summary { padding: 10px 12px; color: var(--accent); font-weight: 600; cursor: pointer; }
.raw-tree { max-height: 20rem; padding: 0 12px 12px; overflow: auto; font-family: var(--font-mono); font-size: 0.78rem; }
.raw-node { margin-left: 12px; padding: 2px 0; }
.raw-node summary { color: var(--accent); cursor: pointer; }
.raw-node code { color: #dbeafe; white-space: pre-wrap; }
```

- [ ] **Step 4: Verify.**

1. Click a link row → drawer slides in from the right; rows behind it do not move; title is `/slug`; QR image, four actions, meta grid, lifecycle entries, and a **collapsed** "Raw DynamoDB item (Urls)" section are present.
2. Expand raw → DynamoDB attribute tree renders. Focus is on the close button when opened; Esc closes and focus returns to the row.
3. Click an analytics event → drawer shows event meta + raw item.
4. Keep the drawer open across ≥2 polls → content stays (no flicker), Hits updates if a short URL is curled meanwhile.

---

### Task 5: Live pipeline hero

**Files:**
- Create: `wwwroot/js/pipeline.js`
- Modify: `wwwroot/js/main.js` (init + update wiring)
- Modify: `wwwroot/styles.css` (append pipeline block)

**Interfaces:**
- Consumes: DOM ids `pipeline-root`, `pipeline-empty`; `changes` object from `state.js`.
- Produces: `init()`, `update(latest)` (called by `main.js` each snapshot), `setOffline(isOffline)` (used by Task 6).

- [ ] **Step 1: Create `wwwroot/js/pipeline.js`:**

```js
const root = document.getElementById("pipeline-root");
const emptyNote = document.getElementById("pipeline-empty");

const NODE_W = 158;
const NODE_H = 42;

function node(id, x, y, label, sub) {
  return `
    <g class="pl-node" id="node-${id}">
      <rect x="${x}" y="${y}" width="${NODE_W}" height="${NODE_H}" rx="9"></rect>
      <text x="${x + NODE_W / 2}" y="${y + (sub ? 18 : 26)}" class="pl-label">${label}</text>
      ${sub ? `<text x="${x + NODE_W / 2}" y="${y + 33}" class="pl-sub">${sub}</text>` : ""}
      <g class="pl-badge" id="badge-${id}" style="display:none">
        <rect x="${x + NODE_W - 18}" y="${y - 10}" width="36" height="18" rx="9"></rect>
        <text x="${x + NODE_W}" y="${y + 3}" id="badge-text-${id}">0</text>
      </g>
    </g>`;
}

function edge(id, d, label, labelX, labelY) {
  return `
    <g>
      <path id="${id}" class="pl-edge" d="${d}" marker-end="url(#pl-arrow)"></path>
      ${label ? `<text x="${labelX}" y="${labelY}" class="pl-edge-label">${label}</text>` : ""}
    </g>`;
}

export function init() {
  root.innerHTML = `
  <svg viewBox="0 0 1128 262" class="pipeline-svg" role="img"
       aria-label="Request pipeline: API Gateway routes to the UrlShortener and Redirector lambdas; DynamoDB Streams feed the QR generator into S3; SQS feeds the Analyzer into the UrlAnalytics table.">
    <defs>
      <marker id="pl-arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
        <path d="M0 0 8 4 0 8z"></path>
      </marker>
    </defs>

    ${edge("e-shorten", "M174 118 C 210 118, 210 61, 238 61", "POST /shorten", 178, 96)}
    ${edge("e-redirect", "M174 142 C 210 142, 210 201, 238 201", "GET /{slug}", 180, 178)}
    ${edge("e-write", "M406 61 H 470", "", 0, 0)}
    ${edge("e-streams", "M630 53 C 668 45, 674 33, 710 31", "DDB Streams", 636, 26)}
    ${edge("e-s3", "M878 31 H 942", "", 0, 0)}
    ${edge("e-created", "M319 82 C 319 140, 380 201, 470 201", "url_created", 326, 148)}
    ${edge("e-accessed", "M406 201 H 470", "url_accessed", 398, 190)}
    ${edge("e-analyzer", "M638 201 H 654", "", 0, 0)}
    ${edge("e-analytics", "M822 201 H 866", "", 0, 0)}

    ${node("apigw", 16, 108, "API Gateway", "emulator")}
    ${node("shortener", 240, 40, "UrlShortener λ")}
    ${node("redirector", 240, 180, "Redirector λ")}
    ${node("urls", 472, 40, "DynamoDB", "Urls")}
    ${node("qr", 712, 10, "QrCodeGenerator λ")}
    ${node("s3", 944, 10, "S3", "qr-bucket")}
    ${node("sqs", 472, 180, "SQS", "url-analytics-events")}
    ${node("analyzer", 656, 180, "Analyzer λ")}
    ${node("analytics", 868, 180, "DynamoDB", "UrlAnalytics")}
  </svg>`;
}

function setBadge(id, count) {
  const badge = document.getElementById(`badge-${id}`);
  const text = document.getElementById(`badge-text-${id}`);
  badge.style.display = count <= 0 ? "none" : "";
  text.textContent = count >= 25 ? "25+" : String(count);
}

function pulse(edgeIds) {
  edgeIds.forEach((edgeId, index) => {
    const element = document.getElementById(edgeId);
    setTimeout(() => {
      element.classList.remove("pl-edge-pulse");
      element.getBoundingClientRect();
      element.classList.add("pl-edge-pulse");
      setTimeout(() => element.classList.remove("pl-edge-pulse"), 1500);
    }, index * 200);
  });
}

export function update(latest) {
  const { links, events, changes, isFirstSnapshot } = latest;

  const isEmpty = links.length === 0 && events.length === 0;
  emptyNote.hidden = !isEmpty;
  root.classList.toggle("pipeline-dim", isEmpty);

  setBadge("urls", links.length);
  setBadge("s3", links.filter((link) => link.qrStatus === "Ready").length);
  setBadge("analytics", events.length);

  const hasPending = links.some((link) => link.qrStatus !== "Ready");
  document.getElementById("node-qr").classList.toggle("pl-node-pending", hasPending);

  if (isFirstSnapshot) {
    return;
  }

  if (changes.newLinkSlugs.length > 0) {
    pulse(["e-shorten", "e-write"]);
  }
  if (changes.qrReadySlugs.length > 0) {
    pulse(["e-streams", "e-s3"]);
  }
  if (changes.createdEventSlugs.length > 0) {
    pulse(["e-created", "e-analyzer", "e-analytics"]);
  }
  if (changes.accessedEventSlugs.length > 0) {
    pulse(["e-redirect", "e-accessed", "e-analyzer", "e-analytics"]);
  }
}

export function setOffline(isOffline) {
  root.classList.toggle("pipeline-dim", isOffline);
}
```

- [ ] **Step 2: Wire into `main.js`.** Add import:

```js
import * as pipeline from "./pipeline.js";
```

Replace `// Task 5 wires: pipeline update here.` with:

```js
    pipeline.update(state.latest);
    pipeline.setOffline(false);
```

Call `pipeline.init();` as the first line of `init()`.

- [ ] **Step 3: Append the pipeline block to `styles.css`:**

```css
/* ---------- Pipeline ---------- */

.pipeline-panel { position: relative; padding: 14px 16px; }

.pipeline-root { transition: opacity 0.3s ease; }
.pipeline-root.pipeline-dim { opacity: 0.35; }

.pipeline-svg { display: block; width: 100%; height: auto; }

.pl-node rect { fill: var(--surface-raised); stroke: var(--border); }
.pl-label { fill: var(--text); font-size: 13px; font-weight: 600; text-anchor: middle; }
.pl-sub { fill: var(--muted); font-size: 10px; text-anchor: middle; }

.pl-node-pending rect { stroke: var(--pending); animation: node-pulse 1.4s ease-in-out infinite; }

@keyframes node-pulse {
  50% { stroke-opacity: 0.35; }
}

.pl-badge rect { fill: var(--accent-dim); stroke: var(--accent); stroke-opacity: 0.5; }
.pl-badge text { fill: var(--accent); font-size: 10.5px; font-weight: 700; text-anchor: middle; }

.pl-edge { fill: none; stroke: var(--border); stroke-width: 1.6; }
#pl-arrow path { fill: var(--border); }

.pl-edge-pulse {
  stroke: var(--accent);
  stroke-width: 2.2;
  stroke-dasharray: 7 9;
  animation: edge-flow 1.5s linear;
}

@keyframes edge-flow {
  from { stroke-dashoffset: 96; }
  to { stroke-dashoffset: 0; }
}

.pl-edge-label { fill: var(--muted); font-size: 10px; }

.pipeline-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  color: var(--text);
  font-size: 0.95rem;
  pointer-events: none;
}

.pipeline-offline {
  position: absolute;
  top: 8px;
  left: 50%;
  transform: translateX(-50%);
  margin: 0;
  padding: 4px 14px;
  border: 1px solid rgba(248, 113, 113, 0.5);
  border-radius: 999px;
  background: rgba(248, 113, 113, 0.12);
  color: var(--danger);
  font-size: 0.76rem;
  font-weight: 600;
}

@media (prefers-reduced-motion: reduce) {
  .pl-edge-pulse, .pl-node-pending rect, .row-enter, .drawer { animation: none; }
  .badge-pending::before { animation: none; border-top-color: var(--pending); }
}
```

- [ ] **Step 4: Verify pipeline behavior.**

1. Reload → the SVG renders all 9 nodes and 9 labeled edges; badges show current counts; with data present the diagram is full-opacity and the empty message hidden.
2. Pulse on create: submit a new URL, then within ~2 s run `browser_evaluate`: `(() => document.getElementById('e-shorten').classList.contains('pl-edge-pulse') || document.getElementById('e-write').classList.contains('pl-edge-pulse'))()` → `true` at least once (retry the check a few times across the window; also confirm visually on screenshot).
3. Pulse on access: `curl` a short URL → the bottom branch (`e-accessed`/`e-analyzer`/`e-analytics`) pulses; `badge-analytics` count increments.
4. Empty state (deterministic, no fresh environment needed): stub the API to return an empty snapshot for a few seconds via `browser_evaluate`:
   `(() => { const orig = window.fetch; window.fetch = (url, opts) => String(url).includes('/api/snapshot') ? Promise.resolve(new Response(JSON.stringify({ links: [], analyticsEvents: [], timelineEvents: [] }), { headers: { 'Content-Type': 'application/json' } })) : orig(url, opts); setTimeout(() => { window.fetch = orig; }, 6000); return true; })()`
   → within ~2 s the pipeline dims (`pipeline-root` has class `pipeline-dim`) and the "Shorten your first URL…" message becomes visible; after 6 s it recovers.
5. Screenshot 1512×945 for the record.

---

### Task 6: Settings popover, visibility pause, error states

**Files:**
- Modify: `wwwroot/js/main.js`

**Interfaces:**
- Consumes: `api.setTraceRefreshRequests`, `pipeline.setOffline`, DOM ids `settings-toggle`, `settings-popover`, `trace-refresh-toggle`, `refresh-now`, `pipeline-offline`.

- [ ] **Step 1: Add to `main.js`.** Add `setTraceRefreshRequests` to the `api.js` import. Add element lookups near the top:

```js
const settingsToggle = document.getElementById("settings-toggle");
const settingsPopover = document.getElementById("settings-popover");
const traceRefreshToggle = document.getElementById("trace-refresh-toggle");
const refreshNow = document.getElementById("refresh-now");
const pipelineOffline = document.getElementById("pipeline-offline");
```

Add popover + polling-lifecycle functions:

```js
function setPopoverOpen(open) {
  settingsPopover.hidden = !open;
  settingsToggle.setAttribute("aria-expanded", String(open));
}

let pollTimer = null;

function startPolling() {
  if (pollTimer === null) {
    pollTimer = setInterval(refresh, POLL_INTERVAL_MS);
  }
}

function stopPolling() {
  if (pollTimer !== null) {
    clearInterval(pollTimer);
    pollTimer = null;
  }
}
```

In `refresh()`: replace the `// Task 6 wires: pipeline offline band here.` comment in the catch block with:

```js
    pipelineOffline.hidden = false;
    pipeline.setOffline(true);
```

and add right after `setOnline(new Date());`:

```js
    pipelineOffline.hidden = true;
```

In `init()` replace `setInterval(refresh, POLL_INTERVAL_MS);` with `startPolling();` and add before `loadConfig`:

```js
  settingsToggle.addEventListener("click", () => setPopoverOpen(settingsPopover.hidden));
  document.addEventListener("click", (event) => {
    if (!settingsPopover.hidden && !event.target.closest(".header-tools")) {
      setPopoverOpen(false);
    }
  });
  traceRefreshToggle.addEventListener("change", () => setTraceRefreshRequests(traceRefreshToggle.checked));
  refreshNow.addEventListener("click", refresh);
  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      stopPolling();
    } else {
      refresh();
      startPolling();
    }
  });
```

Update the existing Esc handler to close the popover first:

```js
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
      return;
    }
    if (!settingsPopover.hidden) {
      setPopoverOpen(false);
      return;
    }
    drawer.close();
  });
```

(Remove the older plain Esc handler from Task 4 so there is exactly one.)

- [ ] **Step 2: Verify.**

1. ⚙ opens/closes the popover (aria-expanded toggles); outside click and Esc close it.
2. Toggle "Trace refresh requests" on → `browser_network_requests` shows subsequent `/api/snapshot` requests with `X-Command-Center-Trace-Refresh: true`.
3. "Refresh now" triggers an immediate snapshot request.
4. Error state: `browser_evaluate` → `(() => { const orig = window.fetch; window.fetch = () => Promise.reject(new Error('offline-sim')); setTimeout(() => { window.fetch = orig; }, 5000); return true; })()` → within ~2 s the dot turns red, status shows "reconnecting…", the offline band appears, the pipeline dims, and **table data stays on screen**. After 5 s everything recovers.
5. Visibility: `browser_tabs` to open/switch to a second tab, wait 5 s, check the Frontend access logs (Aspire MCP `list_console_logs` for Frontend, search `/api/snapshot`) — no requests while hidden; polling resumes on return.

---

### Task 7: Responsive behavior + visual polish pass

**Files:**
- Modify: `wwwroot/styles.css` (append responsive block; adjust values found wanting)

- [ ] **Step 1: Append the responsive block to `styles.css`:**

```css
/* ---------- Responsive ---------- */

@media (max-width: 1100px) {
  .pipeline-panel { overflow-x: auto; }
  .pipeline-svg { min-width: 900px; }
}

@media (max-width: 900px) {
  .app-header { flex-wrap: wrap; }
  .header-status { order: 3; min-width: 0; margin-left: 0; }
  .data-grid { grid-template-columns: 1fr; }
  .shorten-form { max-width: none; }
}
```

- [ ] **Step 2: Cross-width screenshots.** Playwright `browser_resize` + screenshot at **1512×945**, **1280×840**, **900×800**:
- No horizontal page scrollbar at any width (pipeline may scroll inside its own panel below 1100).
- No column overlap anywhere (the old sticky-column bug class).
- No dead space: panels hug their content.

- [ ] **Step 3: Visual polish iteration.** Compare screenshots against the design intent (refined dark, one accent, quiet panels). Adjust CSS values (spacings, font sizes, colors) as needed — structural/behavioral code stays as planned. Re-screenshot until clean. This step is judgment-based; keep changes inside `styles.css`.

---

### Task 8: README update

**Files:**
- Modify: `playground/lambda/README.md`

- [ ] **Step 1: Replace the "Using the Command Center" section** (keep the heading) with:

```markdown
### Using the Command Center

Open the Frontend endpoint from the Aspire Dashboard to use the single-screen Command Center:

- **Shorten bar** posts through the API Gateway emulator and starts both background paths.
- **Live pipeline** mirrors the architecture (API Gateway → lambdas → DynamoDB/S3/SQS). Segments pulse as events flow: creating a link lights the write path, the DynamoDB Streams branch animates when a QR becomes ready, and redirect traffic lights the SQS analytics branch. Node badges show live counts.
- **Links table** shows the `Urls` records with QR status, a thumbnail once generation completes, and a hit counter per link.
- **Analytics events** lists the `AnalyzerLambda` output written from SQS events.
- **Detail drawer** opens when you click any link or event row: QR preview and actions, the link's lifecycle, and the exact raw DynamoDB attribute JSON behind the row.
- **Trace refresh requests** (in the ⚙ settings popover) is off by default so polling reads of `/api/snapshot` do not dominate Aspire traces. Turn it on when you specifically want to debug the Command Center refresh path. Polling also pauses automatically while the tab is hidden.
```

- [ ] **Step 2: Update the two project descriptions.** In "Projects Structure", replace the `LocalStack.Lambda.Frontend` bullet with:

```markdown
- **`LocalStack.Lambda.Frontend`** - ASP.NET Core Command Center page with a live pipeline visualization, link/analytics feeds with per-link hit counts, and a detail drawer exposing raw DynamoDB items
```

In the README, search for remaining "Derived Flow Timeline" / "Urls Table" / "UrlAnalytics Table" mentions inside the Quick Demo section and align them with the new UI names (Links table / Analytics events). Do not touch the architecture diagram or request-flow sections.

- [ ] **Step 3: Verify:** `grep -n "Derived Flow Timeline" playground/lambda/README.md` → no matches.

---

### Task 9: Final verification sweep + handoff (NO commit)

**Files:** none (verification only)

- [ ] **Step 1: Full E2E under `aspire start`** (matches the spec's verification plan):
1. Fresh page load → zero console errors.
2. Create a link → `e-shorten`/`e-write` pulse; QR badge Pending → thumbnail appears; `e-streams`/`e-s3` pulse on the transition.
3. `curl` the short URL → Hits increments, purple event enters the feed, bottom branch pulses.
4. Drawer: open from a link row and an event row; raw JSON tree correct; Esc/backdrop/focus restore work.
5. Strict-mode Playwright click on rows succeeds without retries (diff-render proof).
6. Screenshots at 1512/1280/900 — attach to the session record.

- [ ] **Step 2: Run Slopwatch** (AGENTS.md requirement after LLM-authored changes):

```bash
slopwatch analyze --fail-on warning --exclude "external/**,artifacts/**,**/bin/**,**/obj/**"
```

Expected: no warnings. Fix anything it flags.

- [ ] **Step 3: Present the change summary and proposed commit to Deniz — do NOT commit.** Proposed message:

```
feat(playground): redesign lambda command center with live pipeline view
```

Body bullets: pipeline hero SVG with event-driven pulses; keyed diff-rendering replacing full DOM rebuilds; detail drawer replacing QR modal + inline raw rows; refined dark theme; AccessCount on LinkSummary; README update. Stop and wait for approval.
