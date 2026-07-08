using Amazon.DynamoDBv2.Model;

namespace LocalStack.Lambda.Frontend;

internal static class DynamoDbItemMapper
{
    public static LinkSummary ToLinkSummary(Dictionary<string, AttributeValue> item, int accessCount) => new(
        item["Slug"].S,
        item["Url"].S,
        item["CreatedAt"].S,
        item.TryGetValue("QrStatus", out var status) ? status.S : "Pending",
        item.TryGetValue("QrObjectKey", out var key) ? key.S : null,
        item.TryGetValue("QrGeneratedAt", out var generatedAt) ? generatedAt.S : null,
        accessCount,
        ToRawItem(item));

    public static AnalyticsEventSummary ToAnalyticsEventSummary(Dictionary<string, AttributeValue> item) => new(
        item["EventId"].S,
        item["Timestamp"].S,
        item.TryGetValue("EventType", out var eventType) ? eventType.S : "unknown",
        item.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
        item.TryGetValue("OriginalUrl", out var originalUrl) ? originalUrl.S : string.Empty,
        item.TryGetValue("UserAgent", out var userAgent) ? userAgent.S : "unknown",
        item.TryGetValue("IpAddress", out var ipAddress) ? ipAddress.S : "unknown",
        ToRawItem(item));

    private static Dictionary<string, object?> ToRawItem(Dictionary<string, AttributeValue> item) => item
        .OrderBy(attribute => attribute.Key, StringComparer.Ordinal)
        .ToDictionary(attribute => attribute.Key, attribute => (object?)ToRawAttribute(attribute.Value), StringComparer.Ordinal);

    private static Dictionary<string, object?> ToRawAttribute(AttributeValue value)
    {
        if (value.S is not null)
        {
            return ToSingleAttribute("S", value.S);
        }

        if (value.N is not null)
        {
            return ToSingleAttribute("N", value.N);
        }

        if (value.B is not null)
        {
            return ToSingleAttribute("B", Convert.ToBase64String(value.B.ToArray()));
        }

        if (value.SS is { Count: > 0 })
        {
            return ToSingleAttribute("SS", value.SS.Order(StringComparer.Ordinal).ToArray());
        }

        if (value.NS is { Count: > 0 })
        {
            return ToSingleAttribute("NS", value.NS.Order(StringComparer.Ordinal).ToArray());
        }

        if (value.BS is { Count: > 0 })
        {
            return ToSingleAttribute("BS", value.BS.Select(binary => Convert.ToBase64String(binary.ToArray())).ToArray());
        }

        if (value.M is { Count: > 0 })
        {
            return ToSingleAttribute(
                "M",
                value.M
                    .OrderBy(attribute => attribute.Key, StringComparer.Ordinal)
                    .ToDictionary(attribute => attribute.Key, attribute => (object?)ToRawAttribute(attribute.Value), StringComparer.Ordinal));
        }

        if (value.L is { Count: > 0 })
        {
            return ToSingleAttribute("L", value.L.Select(attribute => (object?)ToRawAttribute(attribute)).ToArray());
        }

        if (value.NULL == true)
        {
            return ToSingleAttribute("NULL", true);
        }

        if (value.IsBOOLSet)
        {
            return ToSingleAttribute("BOOL", value.BOOL);
        }

        return ToSingleAttribute("UNKNOWN", value.ToString());
    }

    private static Dictionary<string, object?> ToSingleAttribute(string typeName, object? value) => new(StringComparer.Ordinal) { [typeName] = value };
}
