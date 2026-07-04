namespace LocalStack.Lambda.Frontend;

internal sealed record ConfigResponse(string ApiGatewayBaseUrl);

internal sealed record LinkSummary(string Slug, string Url, string CreatedAt, string QrStatus, string? QrObjectKey);

internal sealed record AnalyticsEventSummary(string EventId, string Timestamp, string EventType, string Slug, string OriginalUrl);
