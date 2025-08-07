using System.Diagnostics;

namespace LocalStack.Playground.ServiceDefaults.ActivitySources;

public static class RedirectorActivitySource
{
    public const string ActivitySourceName = "LocalStack.Lambda.Redirector";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
