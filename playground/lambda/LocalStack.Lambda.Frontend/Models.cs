namespace LocalStack.Lambda.Frontend;

internal sealed record ConfigResponse(string ApiGatewayBaseUrl);

internal sealed record CommandCenterSnapshot(
    IReadOnlyList<LinkSummary> Links,
    IReadOnlyList<AnalyticsEventSummary> AnalyticsEvents,
    IReadOnlyList<TimelineEventSummary> TimelineEvents);

internal sealed record LinkSummary(
    string Slug,
    string Url,
    string CreatedAt,
    string QrStatus,
    string? QrObjectKey,
    string? QrGeneratedAt,
    IReadOnlyDictionary<string, object?> RawItem);

internal sealed record AnalyticsEventSummary(
    string EventId,
    string Timestamp,
    string EventType,
    string Slug,
    string OriginalUrl,
    string UserAgent,
    string IpAddress,
    IReadOnlyDictionary<string, object?> RawItem);

internal sealed record TimelineEventSummary(
    string Timestamp,
    string EventType,
    string Slug,
    string Description);
