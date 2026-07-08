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
