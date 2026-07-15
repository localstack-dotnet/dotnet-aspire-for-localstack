using System.Globalization;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack.Annotations;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackEndpointConflictWarning
{
    private const string UseLocalStackKey = "LocalStack__UseLocalStack";
    private const string NativeEndpointKey = "AWS_ENDPOINT_URL";
    private const string NativeEndpointPrefix = "AWS_ENDPOINT_URL_";

    internal static void Register(IResourceWithEnvironment resource, ResourceLoggerService loggerService)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(loggerService);

        if (resource.Annotations.Any(static annotation => annotation is LocalStackEndpointConflictWarningAnnotation))
        {
            return;
        }

        resource.Annotations.Add(new LocalStackEndpointConflictWarningAnnotation());

        var logger = loggerService.GetLogger(resource);
        resource.Annotations.Add(new EnvironmentCallbackAnnotation(context => WarnIfConflict(context, resource.Name, logger)));
    }

    private static void WarnIfConflict(EnvironmentCallbackContext context, string resourceName, ILogger logger)
    {
        if (!IsLocalStackClientProxyEnabled(context.EnvironmentVariables) || !HasNativeEndpoint(context.EnvironmentVariables))
        {
            return;
        }

        logger.LogWarning(
            "Resource '{ResourceName}' has both LocalStack.Client proxy routing and native AWS SDK endpoint routing configured. " +
            "Choose either LocalStack.Client proxy routing or native AWS SDK endpoint routing for this workload; remove the other configuration path. " +
            "See the package limitation documentation for details.",
            resourceName);
    }

    private static bool IsLocalStackClientProxyEnabled(Dictionary<string, object> environmentVariables)
    {
        foreach (var (key, value) in environmentVariables)
        {
            if (string.Equals(key, UseLocalStackKey, StringComparison.OrdinalIgnoreCase) &&
                bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var enabled))
            {
                return enabled;
            }
        }

        return false;
    }

    private static bool HasNativeEndpoint(Dictionary<string, object> environmentVariables)
        => environmentVariables.Keys.Any(static key =>
            string.Equals(key, NativeEndpointKey, StringComparison.OrdinalIgnoreCase) ||
            key.StartsWith(NativeEndpointPrefix, StringComparison.OrdinalIgnoreCase));
}
