#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.LocalStack.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring AWS resources to use LocalStack instead of real AWS services.
/// These extensions allow existing Aspire.Hosting.AWS resources to work with LocalStack
/// for local development and testing scenarios.
/// </summary>
public static class LocalStackCloudFormationResourceExtensions
{
    /// <summary>
    /// Configures any AWS CloudFormation resource to use LocalStack instead of real AWS.
    /// This extension works with CloudFormation templates, stacks, and CDK resources that
    /// implement ICloudFormationResource and have a CloudFormationClient property.
    /// </summary>
    /// <typeparam name="T">The CloudFormation resource type that implements ICloudFormationResource.</typeparam>
    /// <param name="builder">The CloudFormation resource builder.</param>
    /// <param name="localStackBuilder">The LocalStack resource to target.</param>
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
    public static IResourceBuilder<T> WithReference<T>(this IResourceBuilder<T> builder, IResourceBuilder<ILocalStackResource>? localStackBuilder)
        where T : class, ICloudFormationResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (localStackBuilder?.Resource.Options.UseLocalStack != true)
        {
            return builder;
        }

        // var localStackOptions = localStackBuilder.Resource.Options;

        // var session = SessionStandalone.Init()
        //     .WithSessionOptions(localStackOptions.Session)
        //     .WithConfigurationOptions(localStackOptions.Config)
        //     .Create();
        //
        // builder.Resource.CloudFormationClient = session.CreateClientByImplementation<AmazonCloudFormationClient>();

        builder.WaitFor(localStackBuilder);
        builder.WithAnnotation(new LocalStackEnabledAnnotation(localStackBuilder.Resource));
        if (!localStackBuilder.Resource.Annotations.Any(x =>
                x is LocalStackReferenceAnnotation referenceAnnotation
                && string.Equals(referenceAnnotation.TargetResource, builder.Resource.Name, StringComparison.Ordinal)))
        {
            localStackBuilder.WithAnnotation(new LocalStackReferenceAnnotation(builder.Resource.Name));
            localStackBuilder.WithAnnotation(new LocalStackReferenceAnnotationV2(builder.Resource));
        }

        return builder;
    }
}
