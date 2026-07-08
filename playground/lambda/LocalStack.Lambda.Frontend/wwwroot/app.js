(() => {
  "use strict";

  const POLL_INTERVAL_MS = 1500;
  const EMPTY_VALUE = "-";

  const state = {
    apiGatewayBaseUrl: "",
    traceRefreshRequests: false,
    links: [],
    analyticsEvents: [],
    expandedRawKeys: new Set(),
    qrLink: null,
    refreshing: false,
    qrReturnFocus: null,
  };

  const shortenForm = document.getElementById("shorten-form");
  const urlInput = document.getElementById("url-input");
  const shortenError = document.getElementById("shorten-error");
  const linksBody = document.getElementById("links-body");
  const analyticsBody = document.getElementById("analytics-body");
  const timelineFeed = document.getElementById("timeline-feed");
  const linksCount = document.getElementById("links-count");
  const analyticsCount = document.getElementById("analytics-count");
  const snapshotStatus = document.getElementById("snapshot-status");
  const refreshNow = document.getElementById("refresh-now");
  const traceRefreshToggle = document.getElementById("trace-refresh-toggle");
  const qrModal = document.getElementById("qr-modal");
  const qrBackdrop = document.getElementById("qr-backdrop");
  const closeQrModal = document.getElementById("close-qr-modal");
  const qrModalImage = document.getElementById("qr-modal-image");
  const qrModalMeta = document.getElementById("qr-modal-meta");
  const qrOpenEndpoint = document.getElementById("qr-open-endpoint");
  const qrOpenShort = document.getElementById("qr-open-short");
  const qrCopyUrl = document.getElementById("qr-copy-url");

  async function fetchJson(url, options = {}) {
    const headers = new Headers(options.headers ?? {});
    headers.set("X-Command-Center-Trace-Refresh", String(state.traceRefreshRequests));

    const response = await fetch(url, { ...options, headers });
    if (!response.ok) {
      const text = await response.text();
      throw new Error(`${url} failed (${response.status}): ${text}`);
    }

    return response.json();
  }

  async function loadConfig() {
    const config = await fetchJson("/api/config");
    state.apiGatewayBaseUrl = trimTrailingSlash(config.apiGatewayBaseUrl);
  }

  function trimTrailingSlash(value) {
    return String(value ?? "").replace(/\/+$/, "");
  }

  function clearChildren(element) {
    while (element.firstChild) {
      element.removeChild(element.firstChild);
    }
  }

  function formatTimestamp(isoString) {
    if (!isoString) {
      return EMPTY_VALUE;
    }

    const date = new Date(isoString);
    return Number.isNaN(date.getTime()) ? isoString : date.toLocaleString();
  }

  function normalizeEventType(eventType) {
    return String(eventType ?? "unknown").replace(/_/g, " ");
  }

  function createTextCell(text, className) {
    const cell = document.createElement("td");
    cell.textContent = text || EMPTY_VALUE;

    if (className) {
      cell.className = className;
    }

    return cell;
  }

  function createBadge(text, modifier) {
    const badge = document.createElement("span");
    badge.className = `badge badge-${modifier}`;
    badge.textContent = text;
    return badge;
  }

  function createActionButton(text, onClick) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "link-button";
    button.textContent = text;
    button.addEventListener("click", onClick);
    return button;
  }

  function isQrReady(link) {
    return String(link.qrStatus ?? "").toLowerCase() === "ready" && Boolean(link.qrObjectKey);
  }

  function shouldIgnoreRowToggle(event) {
    return Boolean(event.target.closest("a, button, details, summary"));
  }

  function toggleRaw(rawKey, render) {
    if (state.expandedRawKeys.has(rawKey)) {
      state.expandedRawKeys.delete(rawKey);
    } else {
      state.expandedRawKeys.add(rawKey);
    }

    render();
  }

  function createRawToggleButton(rawKey, render) {
    const expanded = state.expandedRawKeys.has(rawKey);
    const button = createActionButton(expanded ? "Hide" : "Raw", () => toggleRaw(rawKey, render));
    button.setAttribute("aria-expanded", String(expanded));
    return button;
  }

  function createRawDetailRow(title, rawItem, colSpan) {
    const row = document.createElement("tr");
    row.className = "raw-detail-row";

    const cell = document.createElement("td");
    cell.colSpan = colSpan;

    const details = document.createElement("details");
    details.className = "raw-detail";
    details.open = true;

    const summary = document.createElement("summary");
    summary.textContent = title;
    details.appendChild(summary);

    const actions = document.createElement("div");
    actions.className = "raw-detail-actions";
    actions.appendChild(createActionButton("Copy JSON", () => copyText(JSON.stringify(rawItem, null, 2), "Raw item")));
    details.appendChild(actions);

    const tree = document.createElement("div");
    tree.className = "raw-tree";
    tree.appendChild(renderRawValue("item", rawItem, 0));
    details.appendChild(tree);

    cell.appendChild(details);
    row.appendChild(cell);
    return row;
  }

  function buildQrCell(link) {
    const cell = document.createElement("td");

    if (isQrReady(link)) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "qr-button";
      button.title = `Open QR preview for /${link.slug}`;
      button.addEventListener("click", event => {
        event.stopPropagation();
        openQrModal(link, event.currentTarget);
      });

      const img = document.createElement("img");
      img.className = "qr-image";
      img.alt = `QR code for ${link.slug}`;
      img.src = buildQrUrl(link);

      button.appendChild(img);
      cell.appendChild(button);
    } else {
      cell.appendChild(createBadge("Pending", "pending"));
    }

    return cell;
  }

  function buildQrUrl(link) {
    return `${state.apiGatewayBaseUrl}/${link.slug}/qr`;
  }

  function buildShortUrl(slug) {
    return `${state.apiGatewayBaseUrl}/${slug}`;
  }

  function buildLinkRow(link) {
    const row = document.createElement("tr");
    const rawKey = `link:${link.slug}`;
    const expanded = state.expandedRawKeys.has(rawKey);
    const render = () => renderLinks(state.links);
    row.className = "data-row";
    row.tabIndex = 0;
    row.setAttribute("aria-expanded", String(expanded));
    row.addEventListener("click", event => {
      if (!shouldIgnoreRowToggle(event)) {
        toggleRaw(rawKey, render);
      }
    });
    row.addEventListener("keydown", event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        toggleRaw(rawKey, render);
      }
    });

    const shortCell = document.createElement("td");
    const shortLink = document.createElement("a");
    shortLink.href = buildShortUrl(link.slug);
    shortLink.target = "_blank";
    shortLink.rel = "noopener noreferrer";
    shortLink.textContent = `/${link.slug}`;
    shortLink.addEventListener("click", event => event.stopPropagation());
    shortCell.appendChild(shortLink);

    const statusCell = document.createElement("td");
    statusCell.appendChild(createBadge(link.qrStatus, isQrReady(link) ? "ready" : "pending"));

    const rawCell = document.createElement("td");
    rawCell.appendChild(createRawToggleButton(rawKey, render));

    row.appendChild(shortCell);
    row.appendChild(createTextCell(link.url, "truncate"));
    row.appendChild(createTextCell(formatTimestamp(link.createdAt), "nowrap"));
    row.appendChild(statusCell);
    row.appendChild(createTextCell(formatTimestamp(link.qrGeneratedAt), "nowrap"));
    row.appendChild(buildQrCell(link));
    row.appendChild(rawCell);

    return row;
  }

  function renderLinks(links) {
    state.links = links;
    clearChildren(linksBody);
    linksCount.textContent = `${links.length} ${links.length === 1 ? "link" : "links"}`;

    if (links.length === 0) {
      const row = document.createElement("tr");
      row.className = "empty-row";
      const cell = document.createElement("td");
      cell.colSpan = 7;
      cell.textContent = "No links yet. Shorten one above.";
      row.appendChild(cell);
      linksBody.appendChild(row);
      return;
    }

    for (const link of links) {
      const rawKey = `link:${link.slug}`;
      linksBody.appendChild(buildLinkRow(link));
      if (state.expandedRawKeys.has(rawKey)) {
        linksBody.appendChild(createRawDetailRow(`Urls item /${link.slug}`, link.rawItem, 7));
      }
    }
  }

  function buildAnalyticsRow(analyticsEvent) {
    const row = document.createElement("tr");
    const rawKey = `analytics:${analyticsEvent.eventId}`;
    const expanded = state.expandedRawKeys.has(rawKey);
    const render = () => renderAnalytics(state.analyticsEvents);
    row.className = "data-row";
    row.tabIndex = 0;
    row.setAttribute("aria-expanded", String(expanded));
    row.addEventListener("click", event => {
      if (!shouldIgnoreRowToggle(event)) {
        toggleRaw(rawKey, render);
      }
    });
    row.addEventListener("keydown", event => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        toggleRaw(rawKey, render);
      }
    });

    const eventCell = document.createElement("td");
    const badgeModifier = analyticsEvent.eventType === "url_created" ? "created" : "accessed";
    eventCell.appendChild(createBadge(normalizeEventType(analyticsEvent.eventType), badgeModifier));

    const slugCell = document.createElement("td");
    slugCell.textContent = analyticsEvent.slug ? `/${analyticsEvent.slug}` : EMPTY_VALUE;

    const clientCell = document.createElement("td");
    clientCell.className = "client-cell";
    clientCell.textContent = `${analyticsEvent.ipAddress || "unknown"} | ${analyticsEvent.userAgent || "unknown"}`;
    clientCell.title = clientCell.textContent;

    const rawCell = document.createElement("td");
    rawCell.appendChild(createRawToggleButton(rawKey, render));

    row.appendChild(eventCell);
    row.appendChild(slugCell);
    row.appendChild(createTextCell(analyticsEvent.originalUrl, "truncate"));
    row.appendChild(createTextCell(formatTimestamp(analyticsEvent.timestamp), "nowrap"));
    row.appendChild(clientCell);
    row.appendChild(rawCell);

    return row;
  }

  function renderAnalytics(events) {
    state.analyticsEvents = events;
    clearChildren(analyticsBody);
    analyticsCount.textContent = `${events.length} ${events.length === 1 ? "event" : "events"}`;

    if (events.length === 0) {
      const row = document.createElement("tr");
      row.className = "empty-row";
      const cell = document.createElement("td");
      cell.colSpan = 6;
      cell.textContent = "No analytics events yet.";
      row.appendChild(cell);
      analyticsBody.appendChild(row);
      return;
    }

    for (const analyticsEvent of events) {
      const rawKey = `analytics:${analyticsEvent.eventId}`;
      analyticsBody.appendChild(buildAnalyticsRow(analyticsEvent));
      if (state.expandedRawKeys.has(rawKey)) {
        analyticsBody.appendChild(createRawDetailRow(`UrlAnalytics item ${analyticsEvent.eventId}`, analyticsEvent.rawItem, 6));
      }
    }
  }

  function renderTimeline(events) {
    clearChildren(timelineFeed);

    if (events.length === 0) {
      const item = document.createElement("li");
      item.className = "empty-row";
      item.textContent = "No timeline events yet.";
      timelineFeed.appendChild(item);
      return;
    }

    for (const event of events) {
      const item = document.createElement("li");
      item.className = "timeline-item";

      const marker = document.createElement("span");
      marker.className = `timeline-marker timeline-${event.eventType.toLowerCase()}`;

      const body = document.createElement("div");
      body.className = "timeline-body";

      const title = document.createElement("strong");
      title.textContent = `${event.eventType} /${event.slug}`;

      const description = document.createElement("p");
      description.textContent = event.description;

      const timestamp = document.createElement("time");
      timestamp.textContent = formatTimestamp(event.timestamp);

      body.appendChild(title);
      body.appendChild(description);
      body.appendChild(timestamp);
      item.appendChild(marker);
      item.appendChild(body);
      timelineFeed.appendChild(item);
    }
  }

  async function loadSnapshot() {
    if (state.refreshing) {
      return;
    }

    state.refreshing = true;
    snapshotStatus.textContent = state.traceRefreshRequests ? "Refreshing with traces" : "Refreshing quietly";

    try {
      const snapshot = await fetchJson("/api/snapshot");
      renderLinks(snapshot.links ?? []);
      renderAnalytics(snapshot.analyticsEvents ?? []);
      renderTimeline(snapshot.timelineEvents ?? []);
      snapshotStatus.textContent = `Updated ${new Date().toLocaleTimeString()}`;
      snapshotStatus.classList.remove("status-error");
      snapshotStatus.removeAttribute("title");
    } catch (error) {
      snapshotStatus.textContent = "Refresh failed";
      snapshotStatus.classList.add("status-error");
      snapshotStatus.title = error.message;
    } finally {
      state.refreshing = false;
    }
  }

  async function handleSubmit(submitEvent) {
    submitEvent.preventDefault();
    shortenError.hidden = true;

    const url = urlInput.value.trim();
    if (!url) {
      return;
    }

    try {
      await fetchJson("/api/shorten", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ Url: url }),
      });

      urlInput.value = "";
      await loadSnapshot();
    } catch (error) {
      shortenError.textContent = `Failed to shorten URL: ${error.message}`;
      shortenError.hidden = false;
    }
  }

  function restoreFocus(element) {
    if (element?.isConnected) {
      element.focus();
      return;
    }

    refreshNow.focus();
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

  async function copyText(text, label) {
    try {
      await navigator.clipboard.writeText(text);
      snapshotStatus.textContent = `${label} copied`;
      return;
    } catch {
      if (fallbackCopyText(text)) {
        snapshotStatus.textContent = `${label} copied`;
        return;
      }
    }

    snapshotStatus.textContent = `${label} copy blocked by browser permissions`;
  }

  function fallbackCopyText(text) {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.setAttribute("readonly", "");
    textArea.style.position = "fixed";
    textArea.style.top = "-1000px";
    document.body.appendChild(textArea);
    textArea.select();

    try {
      return document.execCommand("copy");
    } finally {
      textArea.remove();
    }
  }

  function openQrModal(link, triggerElement) {
    state.qrLink = link;
    state.qrReturnFocus = triggerElement;
    const qrUrl = buildQrUrl(link);
    const shortUrl = buildShortUrl(link.slug);

    qrModalImage.src = qrUrl;
    qrModalImage.alt = `Expanded QR code for /${link.slug}`;
    qrModalMeta.textContent = `Slug /${link.slug} | ${link.url} | object ${link.qrObjectKey || EMPTY_VALUE} | generated ${formatTimestamp(link.qrGeneratedAt)}`;
    qrOpenEndpoint.href = qrUrl;
    qrOpenShort.href = shortUrl;
    qrModal.hidden = false;
    qrModal.inert = false;
    qrModal.setAttribute("aria-hidden", "false");
    qrBackdrop.hidden = false;
    closeQrModal.focus();
  }

  function closeQrPreview() {
    qrModal.setAttribute("aria-hidden", "true");
    qrModal.inert = true;
    qrModal.hidden = true;
    qrBackdrop.hidden = true;
    restoreFocus(state.qrReturnFocus);
    state.qrReturnFocus = null;
  }

  function handleKeyDown(event) {
    if (event.key !== "Escape") {
      return;
    }

    if (!qrModal.hidden) {
      closeQrPreview();
      return;
    }

  }

  async function init() {
    qrModal.hidden = true;
    qrModal.inert = true;
    traceRefreshToggle.checked = state.traceRefreshRequests;
    traceRefreshToggle.addEventListener("change", () => {
      state.traceRefreshRequests = traceRefreshToggle.checked;
    });
    shortenForm.addEventListener("submit", handleSubmit);
    refreshNow.addEventListener("click", loadSnapshot);
    closeQrModal.addEventListener("click", closeQrPreview);
    qrBackdrop.addEventListener("click", closeQrPreview);
    qrCopyUrl.addEventListener("click", () => {
      if (state.qrLink === null) {
        snapshotStatus.textContent = "No QR URL selected";
        return;
      }

      copyText(buildQrUrl(state.qrLink), "QR URL");
    });
    document.addEventListener("keydown", handleKeyDown);

    await loadConfig();
    await loadSnapshot();
    setInterval(loadSnapshot, POLL_INTERVAL_MS);
  }

  init().catch((error) => {
    shortenError.textContent = `Failed to initialize command center: ${error.message}`;
    shortenError.hidden = false;
    snapshotStatus.textContent = "Initialization failed";
    snapshotStatus.classList.add("status-error");
  });
})();
