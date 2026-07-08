import { loadConfig, loadSnapshot, shorten, setTraceRefreshRequests } from "./api.js";
import { createStore } from "./state.js";
import { renderLinks, renderEvents } from "./tables.js";
import * as drawer from "./drawer.js";
import * as pipeline from "./pipeline.js";

const POLL_INTERVAL_MS = 1500;

const liveDot = document.getElementById("live-dot");
const liveStatus = document.getElementById("live-status");
const shortenForm = document.getElementById("shorten-form");
const urlInput = document.getElementById("url-input");
const shortenError = document.getElementById("shorten-error");
const settingsToggle = document.getElementById("settings-toggle");
const settingsPopover = document.getElementById("settings-popover");
const traceRefreshToggle = document.getElementById("trace-refresh-toggle");
const refreshNow = document.getElementById("refresh-now");
const pipelineOffline = document.getElementById("pipeline-offline");

const state = {
  apiGatewayBaseUrl: "",
  refreshing: false,
  latest: null,
};

const store = createStore();

export const context = {
  shortUrl: (slug) => `${state.apiGatewayBaseUrl}/${slug}`,
  qrUrl: (slug) => `${state.apiGatewayBaseUrl}/${slug}/qr`,
  copy: copyText,
  openLink: (slug) => {
    drawer.openLink(slug);
    drawer.sync(state.latest, context);
  },
  openEvent: (eventId) => {
    drawer.openEvent(eventId);
    drawer.sync(state.latest, context);
  },
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

async function refresh() {
  if (state.refreshing) {
    return;
  }

  state.refreshing = true;
  try {
    const snapshot = await loadSnapshot();
    state.latest = store.applySnapshot(snapshot);
    renderLinks(state.latest.links, context);
    renderEvents(state.latest.events, context);
    drawer.sync(state.latest, context);
    pipeline.update(state.latest);
    pipeline.setOffline(false);
    setOnline(new Date());
    pipelineOffline.hidden = true;
  } catch {
    setOffline();
    pipelineOffline.hidden = false;
    pipeline.setOffline(true);
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
  pipeline.init();
  shortenForm.addEventListener("submit", handleShorten);
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

  const config = await loadConfig();
  state.apiGatewayBaseUrl = String(config.apiGatewayBaseUrl ?? "").replace(/\/+$/, "");

  await refresh();
  startPolling();
}

init().catch((error) => {
  shortenError.textContent = `Failed to initialize command center: ${error.message}`;
  shortenError.hidden = false;
  setOffline();
});
