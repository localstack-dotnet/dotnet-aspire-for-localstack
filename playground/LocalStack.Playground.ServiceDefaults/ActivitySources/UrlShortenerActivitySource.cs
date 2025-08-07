using System.Diagnostics;

namespace LocalStack.Playground.ServiceDefaults.ActivitySources;

public static class UrlShortenerActivitySource
{
    public const string ActivitySourceName = "LocalStack.Lambda.UrlShortener";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
