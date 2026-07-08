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
