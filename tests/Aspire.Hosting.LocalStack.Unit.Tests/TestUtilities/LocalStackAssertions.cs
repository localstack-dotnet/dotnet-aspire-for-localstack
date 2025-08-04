using System.Reflection;

namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

internal static class LocalStackAssertions
{
    private const string EnvironmentAnnotation = "Aspire.Hosting.ApplicationModel.EnvironmentAnnotation";

    public static void ShouldHaveLocalStackEnabledAnnotation(this IResource resource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
        Assert.Same(expectedLocalStack, annotation.LocalStackResource);
    }

    public static void ShouldHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
    }

    public static void ShouldHaveReferenceToResource(this ILocalStackResource localStackResource, string expectedTargetResourceName)
    {
        ArgumentNullException.ThrowIfNull(localStackResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTargetResourceName);

        var annotation = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .SingleOrDefault(a => string.Equals(a.TargetResource, expectedTargetResourceName, StringComparison.Ordinal));

        Assert.NotNull(annotation);
        Assert.Equal(expectedTargetResourceName, annotation.TargetResource);
    }

    public static void ShouldNotHaveLocalStackEnabledAnnotation(this IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var annotation = resource.Annotations
            .OfType<LocalStackEnabledAnnotation>()
            .SingleOrDefault();

        Assert.Null(annotation);
    }

    public static void ShouldBeConfiguredForLocalStack(this ICloudFormationTemplateResource cfResource)
    {
        ArgumentNullException.ThrowIfNull(cfResource);

        // Should have LocalStack annotation
        cfResource.ShouldHaveLocalStackEnabledAnnotation();

        // CloudFormation client should be configured (not null)
        Assert.NotNull(cfResource.CloudFormationClient);
        Assert.IsType<AmazonCloudFormationClient>(cfResource.CloudFormationClient);
        Assert.NotNull(cfResource.CloudFormationClient.Config.ProxyHost);
        Assert.NotEqual(0, cfResource.CloudFormationClient.Config.ProxyPort);
    }

    public static void ShouldHaveLocalStackEnvironmentConfiguration(this ProjectResource projectResource, ILocalStackResource expectedLocalStack)
    {
        ArgumentNullException.ThrowIfNull(projectResource);
        ArgumentNullException.ThrowIfNull(expectedLocalStack);
        ArgumentNullException.ThrowIfNull(expectedLocalStack.Options);

        // Should have LocalStack annotation
        projectResource.ShouldHaveLocalStackEnabledAnnotation(expectedLocalStack);

        // Should have an environment callback that configures LocalStack variables
        var envAnnotations = projectResource.Annotations
            .Where(ra =>
            {
                var annotationType = ra.GetType();
                var isEnvironmentAnnotation = string.Equals(annotationType.FullName, EnvironmentAnnotation, StringComparison.Ordinal);

                if (!isEnvironmentAnnotation)
                {
                    return false;
                }

                var nameValue = annotationType
                    .GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ra)?.ToString();

                return nameValue?.StartsWith("LocalStack__", StringComparison.Ordinal) ?? false;
            })
            .ToList();

        Assert.NotEmpty(envAnnotations);

        var localStackEnvs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LocalStack__UseLocalStack"] = expectedLocalStack.Options.UseLocalStack.ToString(),
            ["LocalStack__Session__AwsAccessKeyId"] = expectedLocalStack.Options.Session.AwsAccessKeyId,
            ["LocalStack__Session__AwsAccessKey"] = expectedLocalStack.Options.Session.AwsAccessKey,
            ["LocalStack__Session__AwsSessionToken"] = expectedLocalStack.Options.Session.AwsSessionToken,
            ["LocalStack__Session__RegionName"] = expectedLocalStack.Options.Session.RegionName,
            ["LocalStack__Config__LocalStackHost"] = expectedLocalStack.Options.Config.LocalStackHost,
            ["LocalStack__Config__UseSsl"] = expectedLocalStack.Options.Config.UseSsl.ToString(),
            ["LocalStack__Config__UseLegacyPorts"] = expectedLocalStack.Options.Config.UseLegacyPorts.ToString(),
            ["LocalStack__Config__EdgePort"] = expectedLocalStack.Options.Config.EdgePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        Assert.All(envAnnotations, annotation =>
        {
            var nameField = annotation.GetType().GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);
            var valueField = annotation.GetType().GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(nameField);
            Assert.NotNull(valueField);

            var nameValue = nameField.GetValue(annotation)?.ToString();
            var valueValue = valueField.GetValue(annotation)?.ToString();
            Assert.NotNull(nameValue);
            Assert.NotNull(valueValue);

            Assert.True(localStackEnvs.ContainsKey(nameValue));
            Assert.Equal(valueValue, localStackEnvs[nameValue]);
        });
    }

    public static void ShouldHaveResourceCount<T>(this DistributedApplication app, int expectedCount)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(app);

        var actualCount = app.GetResources<T>().Count();
        Assert.Equal(expectedCount, actualCount);
    }

    public static void ShouldWaitFor(this IResource resource, IResource dependencyResource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(dependencyResource);

        var tryGetAnnotationsOfType = resource.TryGetAnnotationsOfType<WaitAnnotation>(out var annotations);
        Assert.True(tryGetAnnotationsOfType);
        Assert.NotNull(annotations);

        var hasWaitForDependencyResource = annotations.Any(annotation => annotation.Resource == dependencyResource);
        Assert.True(hasWaitForDependencyResource, $"Resource '{resource.Name}' should wait for resource '{dependencyResource.Name}'.");
    }
}
