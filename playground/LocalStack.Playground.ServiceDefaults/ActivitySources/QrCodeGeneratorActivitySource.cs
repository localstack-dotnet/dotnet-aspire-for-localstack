using System.Diagnostics;

namespace LocalStack.Playground.ServiceDefaults.ActivitySources;

public static class QrCodeGeneratorActivitySource
{
    public const string ActivitySourceName = "LocalStack.Lambda.QrCodeGenerator";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
