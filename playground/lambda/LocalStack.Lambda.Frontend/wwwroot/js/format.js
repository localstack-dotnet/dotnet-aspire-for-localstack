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
