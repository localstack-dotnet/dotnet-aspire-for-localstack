# Lambda Frontend (Command Center) Redesign — Design

Date: 2026-07-08
Status: Approved design, pending implementation plan
Scope: `playground/lambda/LocalStack.Lambda.Frontend` (primarily `wwwroot/`, one small `Program.cs` addition), plus `playground/lambda/README.md` updates

## Purpose And Decisions

The Command Center is the demo face of the lambda playground. Its job is to make the two
asynchronous event paths (DynamoDB Streams → QR generation, SQS → analytics) visible and
impressive — README/blog screenshot value included. Decisions agreed with Deniz:

- **Primary purpose**: demo/showcase (not a daily debugging tool).
- **Stack**: vanilla ES modules + CSS, no npm/build chain, everything self-contained under `wwwroot/`.
- **Theme**: refined dark (single dark surface family, one teal accent, disciplined type/spacing).
- **Approach**: "Pipeline Hero" — a live pipeline visualization on top, compact tables below,
  a right-side drawer for per-link detail.

## Problems Being Fixed

Observed via Playwright against the running AppHost (2026-07-08):

1. **Full DOM rebuild every 1.5 s poll** — elements constantly detached; Playwright strict clicks
   fail with "element is not stable"; humans get missed clicks, lost text selection, flicker.
2. **Grid layout bug** — the "Create a Short URL" panel balloons with dead space as the timeline
   column grows (row stretch + `align-items: end`).
3. **Sticky column overlap** — the right-stuck QR/Raw columns cover Created/QR Status/Generated;
   at 1280 px those columns are completely hidden.
4. **Raw detail row** pushes other rows out of view; tree starts fully expanded.
5. **Redundant information** — timeline emits three near-identical entries per link
   (LinkCreated + AnalyticsRecorded + QrReady); Client column is usually "unknown | unknown".
6. Minor: favicon 404 (only console error), long locale timestamps everywhere, header controls
   wrap/jump at 1280 px, huge dead space in the empty state, no access counts shown.

## Layout

Single screen, no scroll at typical desktop widths:

```
┌──────────────────────────────────────────────────────────────────┐
│ ⚡ URL Shortener — Command Center    ● live, 2s ago  [⚙ settings]│  ← slim header
│ [https://…                                    ] [Shorten]        │  ← form under header
├──────────────────────────────────────────────────────────────────┤
│                        LIVE PIPELINE (SVG)                        │
│ POST /shorten                    ┌─▶ DDB Streams ─▶ QR λ ─▶ S3   │
│  [API GW] ─▶ [Shortener λ] ─▶ [Urls]                              │
│                    │             └──────────┐                     │
│ GET /{slug}        └─▶ url_created ─▶ [SQS]─┴▶ [Analyzer λ] ─▶   │
│  [Redirector λ] ──▶ url_accessed ──▶─┘         [UrlAnalytics]    │
├───────────────────────────────────┬──────────────────────────────┤
│ LINKS (n)                         │ ANALYTICS EVENTS (n)          │
│ table — row click → drawer        │ compact event feed            │
└───────────────────────────────────┴──────────────────────────────┘
```

- **Header** collapses to one row: small title, live-status indicator (green dot + fixed-width
  "updated Xs ago" that never causes layout shift), and a ⚙ popover containing the
  "Trace refresh requests" toggle and "Refresh now" (both features stay; they leave prime real
  estate). The shorten form sits directly below as the screen's most prominent action.
  Favicon added (inline SVG data URI) — kills the only console error.
- **Pipeline hero**: hand-authored fixed-topology SVG mirroring the real architecture, including
  the Redirector as the `GET /{slug}` entry point feeding `url_accessed` into SQS (without it,
  half the SQS branch is unexplained).
- **Empty state**: the pipeline renders dimmed with a centered "Shorten your first URL to watch
  it flow through the pipeline" message — no dead space even before the first link.
- The separate "Derived Flow Timeline" panel is removed. Its role is absorbed by the pipeline
  animations, the per-link lifecycle inside the drawer, and the analytics feed. README updated
  accordingly.

## Live Pipeline Behavior

- The client diffs each `/api/snapshot` against the previous one:
  - new link → the Shortener → Urls segment pulses;
  - a link's QrStatus transitions Pending → Ready → the Streams branch animates end-to-end;
  - new `url_accessed` event → the SQS branch pulses.
- Live counter badges on nodes: link count on Urls, ready-QR count on S3, event count on
  UrlAnalytics. The QR λ node pulses softly while any link is Pending.
- `prefers-reduced-motion` disables animations; counters remain.

## Tables And Drawer

**Links table** (left): Short (`/slug` + hover copy button) · Original URL (truncated, full in
tooltip) · Created (relative time, absolute in tooltip) · QR (thumbnail or spinning pending
badge) · Hits (new). No sticky-column hack; the URL column flexes, others stay fixed-width, and
nothing overlaps at narrow widths.

**Drawer** (right slide-over, replaces both the QR modal and the inline raw row): large QR
preview + actions (open/copy short URL, open QR endpoint), the link's own lifecycle mini-timeline
(created → QR ready → accesses), and the raw DynamoDB item tree (collapsed by default). Other
rows never move. Esc/backdrop closes; focus management preserved.

**Analytics feed** (right panel): compact event list — badge (created/accessed), slug, relative
time; client IP/UA only in a tooltip. Clicking an event opens its raw item in the same drawer.

## Render Architecture

- **Polling stays** (1.5 s, `/api/snapshot`). SSE/WebSocket is out of scope: the
  trace-suppression machinery (`CommandCenterRefreshTelemetry`,
  `X-Command-Center-Trace-Refresh`) is built around polling and is itself a demo feature.
- **Full DOM rebuild ends**: rows are keyed by slug/eventId; only changed cells update; new rows
  enter at the top with a brief highlight; removed rows leave. This is the root fix for element
  instability.
- Polling pauses while the tab is hidden (`visibilitychange`) — also reduces Aspire trace noise.
- Code layout: `wwwroot/js/` ES modules (`api.js`, `state.js`, `pipeline.js`, `tables.js`,
  `drawer.js`, …) loaded via `<script type="module">`; still no build step.

## Backend Change (only one)

`LinkSummary` gains an `AccessCount` field. `Program.cs` computes per-slug `url_accessed` counts
from the full analytics scan result (counted before the 25-item feed cap is applied). No new
endpoints; the snapshot shape only widens.

## Visual System

- One dark surface family (current navy tones simplified), one teal accent (existing `#5eead4`
  family), status colors only where meaningful: green=ready/created, amber=pending,
  purple=accessed. Panels become flat dark surfaces with thin borders; the current
  per-panel gradients/blur/glow mix is removed; shadows minimal.
- Type scale: title ~1.25 rem (replacing the `clamp(2rem,4vw,4rem)` headline), panel headings
  ~0.95 rem uppercase-muted, body 0.875 rem, monospace only for slugs/raw JSON. Spacing on a
  4 px base scale.
- The pipeline SVG uses the same language: nodes as panel-style boxes, active segments glow in
  the accent color, counter badges share the table badge component.

## Error Handling

- Snapshot failure: status indicator turns red; the pipeline dims with a "connection lost,
  retrying" band; existing data stays on screen.
- Shorten failure (e.g. API GW 502): inline, non-auto-dismissing message under the form (current
  behavior kept).
- QR image load failure: "QR unavailable" badge instead of a broken image.

## Verification Plan

1. Under `aspire start`, drive end-to-end with Playwright: create a link → pipeline segment
   pulses; QR pending → ready transition animates; hit counter increments after a curl access;
   drawer opens with raw JSON.
2. Zero console errors (favicon included); screenshots at 1512/1280/900 px — no column overlap,
   no dead space.
3. Element stability during polling: Playwright strict clicks succeed without retries
   (the proof that diff-rendering works).
4. `playground/lambda/README.md` "Using the Command Center" section updated for the new UI.

## Out Of Scope (deliberate)

SSE/WebSocket transport, light theme, mobile-first design (below 900 px panels stack and remain
functional, nothing more), any backend change beyond `AccessCount`.
