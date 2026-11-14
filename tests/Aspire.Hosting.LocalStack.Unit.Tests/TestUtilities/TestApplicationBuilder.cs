using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class TestApplicationBuilder
{
    /// <summary>
    /// Creates an AppHost builder that does not read the machine's environment variables.
    /// The package reads <c>LOCALSTACK_AUTH_TOKEN</c> and the <c>LocalStack</c> and
    /// <c>Aspire:Hosting:LocalStack</c> sections from configuration, so a developer's
    /// ambient values would otherwise change what a unit test observes. Command-line
    /// arguments still apply; tests set any other value explicitly.
    /// </summary>
    public static IDistributedApplicationBuilder CreateBuilder(string[] args)
    {
#pragma warning disable RS0030 // The one sanctioned call; every other unit test goes through this method.
        var builder = DistributedApplication.CreateBuilder(args);
#pragma warning restore RS0030
        var sources = builder.Configuration.Sources;
        for (var index = sources.Count - 1; index >= 0; index--)
        {
            if (sources[index] is EnvironmentVariablesConfigurationSource)
            {
                sources.RemoveAt(index);
            }
        }

        return builder;
    }

    public static DistributedApplication Create(Action<IDistributedApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = CreateBuilder([]);
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
