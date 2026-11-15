using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackHostingStateProviderExtensions
{
    public static LocalStackHostingState GetHostingState(this ILocalStackResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource is ILocalStackHostingStateProvider provider)
        {
            return provider.HostingState;
        }

        // A resource implemented outside the package can only describe itself through the
        // deprecated options contract, so that remains its source of hosting state.
#pragma warning disable CS0618
        return LocalStackHostingState.FromLocalStackOptions(resource.Options);
#pragma warning restore CS0618
    }
}
