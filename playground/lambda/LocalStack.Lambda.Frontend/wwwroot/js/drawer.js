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
  renderedSignature: null,
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
  state.renderedSignature = null;
  if (state.returnFocus?.isConnected) {
    state.returnFocus.focus();
  }
  state.returnFocus = null;
}

function show() {
  state.returnFocus = document.activeElement;
  state.renderedSignature = null;
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
    const relatedEvents = latest.events.filter((candidate) => candidate.slug === link.slug);
    renderIfChanged(JSON.stringify([link, relatedEvents]), () => renderLink(link, relatedEvents, context));
  } else {
    const event = latest.events.find((candidate) => candidate.eventId === state.key);
    if (!event) {
      close();
      return;
    }
    renderIfChanged(JSON.stringify(event), () => renderEvent(event));
  }
}

function renderIfChanged(signature, render) {
  if (state.renderedSignature === signature) {
    return;
  }

  const rawWasOpen = content.querySelector(".raw-detail")?.open ?? false;
  render();
  if (rawWasOpen) {
    content.querySelector(".raw-detail").open = true;
  }
  state.renderedSignature = signature;
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
