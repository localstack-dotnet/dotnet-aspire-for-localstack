namespace LocalStack.Provisioning.Frontend.Services;

internal static class TimeFormatting
{
    public static string Relative(DateTimeOffset timestamp, DateTimeOffset? now = null)
    {
        var reference = now ?? DateTimeOffset.UtcNow;
        var seconds = Math.Round((reference - timestamp).TotalSeconds);

        if (seconds < 5)
        {
            return "just now";
        }

        if (seconds < 60)
        {
            return $"{seconds:0}s ago";
        }

        var minutes = Math.Round(seconds / 60);
        if (minutes < 60)
        {
            return $"{minutes:0}m ago";
        }

        var hours = Math.Round(minutes / 60);
        return hours < 24 ? $"{hours:0}h ago" : timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }
}
