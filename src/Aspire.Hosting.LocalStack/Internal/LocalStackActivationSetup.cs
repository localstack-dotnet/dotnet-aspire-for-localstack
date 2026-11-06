using System.Globalization;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackActivationSetup
{
    internal static void ApplyAdditionalEnvironment(
        IResourceBuilder<LocalStackResource> resource,
        LocalStackContainerOptions options)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var (key, value) in options.AdditionalEnvironmentVariables)
        {
            if (!string.Equals(key, LocalStackAuthTokenResolver.EnvironmentVariableName, StringComparison.Ordinal))
            {
                resource.WithEnvironment(key, value);
            }
        }
    }

    internal static void ConfigureActivation(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<LocalStackResource> resource,
        LocalStackContainerOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(options);

        if (builder.ExecutionContext.IsRunMode)
        {
            var source = LocalStackAuthTokenResolver.Resolve(options, builder.Configuration);
            ApplyToken(builder, resource, source);
            RegisterGuard(builder, resource, LocalStackRuntimeCatalog.Production);
        }
    }

    internal static void RegisterGuard(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<LocalStackResource> resource,
        LocalStackRuntimeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!builder.ExecutionContext.IsRunMode)
        {
            return;
        }

        var state = GetState(resource.Resource);
        if (state.GuardRegistered)
        {
            return;
        }

        state.GuardRegistered = true;
        builder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
        {
            if (!state.GuardAttached)
            {
                RefreshEnvironmentCallbackGeneration(resource.Resource);
                resource.WithEnvironment(context => ValidateEnvironment(context, catalog));
                state.GuardAttached = true;
            }

            return Task.CompletedTask;
        });
    }

    private static void RefreshEnvironmentCallbackGeneration(LocalStackResource resource)
    {
        // Core's per-callback cache retains the first context's dictionary, not the next
        // callback's context. Finalizing with a new guard needs a fresh shared generation.
        // Keep delegate order and references, without reading private caches or resolving values.
        for (var index = 0; index < resource.Annotations.Count; index++)
        {
            if (resource.Annotations[index] is EnvironmentCallbackAnnotation environment)
            {
                resource.Annotations[index] = new EnvironmentCallbackAnnotation(environment.Callback);
            }
        }
    }

    internal static void ValidateEnvironment(EnvironmentCallbackContext context, LocalStackRuntimeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!context.ExecutionContext.IsRunMode || context.Resource is not LocalStackResource localStack)
        {
            return;
        }

        var annotation = localStack.Annotations.OfType<ContainerImageAnnotation>().LastOrDefault();
        if (annotation is null || LocalStackRuntimeResolver.Classify(
                LocalStackRuntimeResolver.FromAnnotation(annotation), catalog) != LocalStackRuntimeKind.KnownCurrent)
        {
            return;
        }

        if (!context.EnvironmentVariables.TryGetValue(LocalStackAuthTokenResolver.EnvironmentVariableName, out var binding)
            || binding is null || (binding is string literal && string.IsNullOrWhiteSpace(literal)))
        {
            throw new DistributedApplicationException(
                $"LocalStack resource '{localStack.Name}' requires '{LocalStackAuthTokenResolver.EnvironmentVariableName}'. " +
                "Configure WithAuthToken or a secret parameter environment binding.");
        }
    }

    internal static ParameterResource? ApplyToken(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<LocalStackResource> resource,
        LocalStackAuthTokenSource? source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        if (!builder.ExecutionContext.IsRunMode)
        {
            return null;
        }

        var state = GetState(resource.Resource);
        if (state.TokenApplied)
        {
            return state.Parameter;
        }

        state.TokenApplied = true;
        if (source?.Parameter is { } callerParameter)
        {
            resource.WithEnvironment(LocalStackAuthTokenResolver.EnvironmentVariableName, callerParameter);
            state.Parameter = callerParameter.Resource;
            return callerParameter.Resource;
        }

        if (source?.Literal is not { } literal || string.IsNullOrWhiteSpace(literal))
        {
            return null;
        }

        var name = GetParameterName(builder, resource.Resource.Name);
        var parameter = builder.AddParameter(name, () => literal, secret: true);
        resource.WithEnvironment(LocalStackAuthTokenResolver.EnvironmentVariableName, parameter);
        state.Parameter = parameter.Resource;
        return parameter.Resource;
    }

    private static string GetParameterName(IDistributedApplicationBuilder builder, string resourceName)
    {
        var baseName = "ls-" + resourceName[..Math.Min(40, resourceName.Length)].TrimEnd('-') + "-auth";
        var candidate = baseName;
        var suffix = 1;
        while (builder.Resources.Any(resource => string.Equals(resource.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = baseName + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            suffix++;
        }

        return candidate;
    }

    private static ActivationStateAnnotation GetState(LocalStackResource resource)
    {
        var state = resource.Annotations.OfType<ActivationStateAnnotation>().SingleOrDefault();
        if (state is null)
        {
            state = new ActivationStateAnnotation();
            resource.Annotations.Add(state);
        }

        return state;
    }

    private sealed class ActivationStateAnnotation : IResourceAnnotation
    {
        internal bool TokenApplied { get; set; }

        internal ParameterResource? Parameter { get; set; }

        internal bool GuardRegistered { get; set; }

        internal bool GuardAttached { get; set; }
    }
}
