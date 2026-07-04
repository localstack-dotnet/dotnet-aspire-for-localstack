(() => {
  "use strict";

  const POLL_INTERVAL_MS = 1500;

  const state = {
    apiGatewayBaseUrl: "",
  };

  const shortenForm = document.getElementById("shorten-form");
  const urlInput = document.getElementById("url-input");
  const shortenError = document.getElementById("shorten-error");
  const linksBody = document.getElementById("links-body");
  const analyticsFeed = document.getElementById("analytics-feed");

  async function loadConfig() {
    const response = await fetch("/api/config");
    if (!response.ok) {
      throw new Error(`Failed to load config: ${response.status}`);
    }

    const config = await response.json();
    state.apiGatewayBaseUrl = config.apiGatewayBaseUrl;
  }

  function clearChildren(element) {
    while (element.firstChild) {
      element.removeChild(element.firstChild);
    }
  }

  function formatTimestamp(isoString) {
    const date = new Date(isoString);
    return Number.isNaN(date.getTime()) ? isoString : date.toLocaleString();
  }

  function buildQrCell(link) {
    const cell = document.createElement("td");

    if (link.qrStatus === "Ready") {
      const img = document.createElement("img");
      img.className = "qr-image";
      img.alt = `QR code for ${link.slug}`;
      img.src = `${state.apiGatewayBaseUrl}/${link.slug}/qr`;
      cell.appendChild(img);
    } else {
      const badge = document.createElement("span");
      badge.className = "badge badge-pending";
      badge.textContent = "Pending";
      cell.appendChild(badge);
    }

    return cell;
  }

  function buildLinkRow(link) {
    const row = document.createElement("tr");

    const shortCell = document.createElement("td");
    const shortLink = document.createElement("a");
    shortLink.href = `${state.apiGatewayBaseUrl}/${link.slug}`;
    shortLink.target = "_blank";
    shortLink.rel = "noopener noreferrer";
    shortLink.textContent = `/${link.slug}`;
    shortCell.appendChild(shortLink);

    const urlCell = document.createElement("td");
    urlCell.textContent = link.url;
    urlCell.className = "truncate";
    urlCell.title = link.url;

    const createdCell = document.createElement("td");
    createdCell.textContent = formatTimestamp(link.createdAt);

    row.appendChild(shortCell);
    row.appendChild(urlCell);
    row.appendChild(createdCell);
    row.appendChild(buildQrCell(link));

    return row;
  }

  function renderLinks(links) {
    clearChildren(linksBody);

    if (links.length === 0) {
      const row = document.createElement("tr");
      row.className = "empty-row";
      const cell = document.createElement("td");
      cell.colSpan = 4;
      cell.textContent = "No links yet. Shorten one above.";
      row.appendChild(cell);
      linksBody.appendChild(row);
      return;
    }

    for (const link of links) {
      linksBody.appendChild(buildLinkRow(link));
    }
  }

  function buildAnalyticsItem(analyticsEvent) {
    const item = document.createElement("li");
    item.className = "analytics-item";

    const badgeModifier = analyticsEvent.eventType === "url_created" ? "created" : "accessed";
    const badge = document.createElement("span");
    badge.className = `badge badge-${badgeModifier}`;
    badge.textContent = analyticsEvent.eventType;

    const details = document.createElement("span");
    details.className = "analytics-details";
    details.textContent = `/${analyticsEvent.slug} - ${formatTimestamp(analyticsEvent.timestamp)}`;

    item.appendChild(badge);
    item.appendChild(details);

    return item;
  }

  function renderAnalytics(events) {
    clearChildren(analyticsFeed);

    if (events.length === 0) {
      const item = document.createElement("li");
      item.className = "empty-row";
      item.textContent = "No events yet.";
      analyticsFeed.appendChild(item);
      return;
    }

    for (const analyticsEvent of events) {
      analyticsFeed.appendChild(buildAnalyticsItem(analyticsEvent));
    }
  }

  async function loadLinks() {
    const response = await fetch("/api/links");
    if (!response.ok) {
      return;
    }

    renderLinks(await response.json());
  }

  async function loadAnalytics() {
    const response = await fetch("/api/analytics");
    if (!response.ok) {
      return;
    }

    renderAnalytics(await response.json());
  }

  async function pollFeeds() {
    await Promise.all([loadLinks(), loadAnalytics()]);
  }

  async function handleSubmit(submitEvent) {
    submitEvent.preventDefault();
    shortenError.hidden = true;

    const url = urlInput.value.trim();
    if (!url) {
      return;
    }

    try {
      const response = await fetch("/api/shorten", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ Url: url }),
      });

      if (!response.ok) {
        const text = await response.text();
        shortenError.textContent = `Failed to shorten URL (${response.status}): ${text}`;
        shortenError.hidden = false;
        return;
      }

      urlInput.value = "";
      await loadLinks();
    } catch (error) {
      shortenError.textContent = `Failed to reach the control room API: ${error.message}`;
      shortenError.hidden = false;
    }
  }

  async function init() {
    await loadConfig();
    await pollFeeds();
    shortenForm.addEventListener("submit", handleSubmit);
    setInterval(pollFeeds, POLL_INTERVAL_MS);
  }

  init().catch((error) => {
    shortenError.textContent = `Failed to initialize control room: ${error.message}`;
    shortenError.hidden = false;
  });
})();
