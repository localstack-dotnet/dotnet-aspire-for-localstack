const root = document.getElementById("pipeline-root");
const emptyNote = document.getElementById("pipeline-empty");

let dimBecauseEmpty = false;
let dimBecauseOffline = false;

function applyDim() {
  root.classList.toggle("pipeline-dim", dimBecauseEmpty || dimBecauseOffline);
}

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
  dimBecauseEmpty = isEmpty;
  applyDim();

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
  dimBecauseOffline = isOffline;
  applyDim();
}
