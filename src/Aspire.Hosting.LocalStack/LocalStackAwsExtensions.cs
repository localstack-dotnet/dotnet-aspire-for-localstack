#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Amazon.CloudFormation;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;
using LocalStack.Client;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring AWS resources to use LocalStack instead of real AWS services.
/// These extensions allow existing Aspire.Hosting.AWS resources to work with LocalStack
/// for local development and testing scenarios.
/// </summary>
public static class LocalStackAwsExtensions
{
    /// <summary>
    /// Configures any AWS CloudFormation resource to use LocalStack instead of real AWS.
    /// This extension works with CloudFormation templates, stacks, and CDK resources that
    /// implement ICloudFormationResource and have a CloudFormationClient property.
    /// </summary>
    /// <typeparam name="T">The CloudFormation resource type that implements ICloudFormationResource.</typeparam>
    /// <param name="builder">The CloudFormation resource builder.</param>
    /// <param name="localStack">The LocalStack resource to target.</param>
    /// <returns>The same resource builder for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// var localStack = builder.AddLocalStack();
    ///
    /// // Works with CloudFormation templates
    /// var template = builder.AddAWSCloudFormationTemplate("infrastructure", "template.yaml")
    ///     .WithLocalStack(localStack);
    ///
    /// // Works with CDK stacks
    /// var stack = builder.AddAWSCDKStack("my-stack", (scope) => new MyStack(scope, "MyStack"))
    ///     .WithLocalStack(localStack);
    /// </code>
    /// </example>
    public static IResourceBuilder<T> WithLocalStack<T>(this IResourceBuilder<T> builder, IResourceBuilder<LocalStackResource> localStack)
        where T : class, ICloudFormationResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(localStack);

        var localStackOptions = localStack.Resource.Options;

        if (!localStackOptions.UseLocalStack)
        {
            return builder;
        }

        var session = SessionStandalone.Init()
            .WithSessionOptions(localStackOptions.Session)
            .WithConfigurationOptions(localStackOptions.Config)
            .Create();

        builder.Resource.CloudFormationClient = session.CreateClientByImplementation<AmazonCloudFormationClient>();

        builder.WithAnnotation(new LocalStackEnabledAnnotation(localStack.Resource));

        return builder;
    }
}

/// <summary>
/// Annotation to mark that a resource has been configured to use LocalStack.
/// This enables tooling and debugging support to understand LocalStack dependencies.
/// </summary>
/// <param name="localStackResource">The LocalStack resource this AWS resource depends on.</param>
internal sealed class LocalStackEnabledAnnotation(LocalStackResource localStackResource) : IResourceAnnotation
{
    /// <summary>
    /// The LocalStack resource that this AWS resource is configured to use.
    /// </summary>
    public LocalStackResource LocalStackResource { get; } = localStackResource;
}
