using Amazon.DynamoDBv2.Model;

namespace LocalStack.Lambda.Frontend;

internal static class DynamoDbItemMapper
{
    public static LinkSummary ToLinkSummary(Dictionary<string, AttributeValue> item) => new(
        item["Slug"].S,
        item["Url"].S,
        item["CreatedAt"].S,
        item.TryGetValue("QrStatus", out var status) ? status.S : "Pending",
        item.TryGetValue("QrObjectKey", out var key) ? key.S : null);

    public static AnalyticsEventSummary ToAnalyticsEventSummary(Dictionary<string, AttributeValue> item) => new(
        item["EventId"].S,
        item["Timestamp"].S,
        item.TryGetValue("EventType", out var eventType) ? eventType.S : "unknown",
        item.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
        item.TryGetValue("OriginalUrl", out var originalUrl) ? originalUrl.S : string.Empty);
}
