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
