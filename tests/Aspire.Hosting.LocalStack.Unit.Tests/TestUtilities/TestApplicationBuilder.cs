namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class TestApplicationBuilder
{
    public static DistributedApplication Create(Action<IDistributedApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = DistributedApplication.CreateBuilder([]);
        configure(builder);
        return builder.Build(); // Build but never Run()
    }

    public static (DistributedApplication app, T resource) CreateWithResource<T>(string resourceName, Action<IDistributedApplicationBuilder> configure)
        where T : IResource
    {
        var app = Create(configure);
        var resource = app.GetResource<T>(resourceName);
        return (app, resource);
    }

    public static T GetResource<T>(this DistributedApplication app, string resourceName)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources
                           .OfType<T>()
                           .SingleOrDefault(r => string.Equals(r.Name, resourceName, StringComparison.Ordinal)) ??
                       throw new InvalidOperationException($"Resource '{resourceName}' of type '{typeof(T).Name}' not found in application model.");
        return resource;
    }

    public static IEnumerable<T> GetResources<T>(this DistributedApplication app)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return model.Resources.OfType<T>();
    }

    public static bool HasResource<T>(this DistributedApplication app, string resourceName)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return model.Resources
            .OfType<T>()
            .Any(r => string.Equals(r.Name, resourceName, StringComparison.Ordinal));
    }
}
