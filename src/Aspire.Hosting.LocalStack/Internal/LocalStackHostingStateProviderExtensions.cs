using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackHostingStateProviderExtensions
{
    public static LocalStackHostingState GetHostingState(this ILocalStackResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return resource is ILocalStackHostingStateProvider provider
            ? provider.HostingState
            : throw new InvalidOperationException(
                $"LocalStack resource '{resource.Name}' does not expose package-owned hosting state.");
    }
}
