using LocalStack.Client.Options;

namespace LocalStack.Provisioning.Frontend.Services;

/// <summary>
/// Produces the endpoint text shown in the UI. The SDK's DetermineServiceOperationEndpoint
/// reports the regional AWS endpoint even when the client is routed to LocalStack via
/// ServiceURL, so the display is derived from the LocalStack options instead.
/// </summary>
internal static class EndpointDisplay
{
    public static string Resolve(LocalStackOptions options, string regionSystemName, string serviceName)
    {
        if (!options.UseLocalStack)
        {
            return $"https://{serviceName}.{regionSystemName}.amazonaws.com/";
        }

        var scheme = options.Config.UseSsl ? "https" : "http";
        return $"{scheme}://{options.Config.LocalStackHost}:{options.Config.EdgePort} (LocalStack edge)";
    }
}
