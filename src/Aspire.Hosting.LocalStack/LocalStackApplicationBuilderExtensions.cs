#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AWS.CDK;
using Aspire.Hosting.AWS.CloudFormation;
using Aspire.Hosting.LocalStack.CDK;

namespace Aspire.Hosting;

/// <summary>
/// Extensions for configuring all AWS resources in an application to use LocalStack.
/// </summary>
public static class LocalStackApplicationBuilderExtensions
{
    /// <summary>
    /// Configures all AWS resources in the application to use the specified LocalStack instance.
    /// Automatically detects CloudFormation templates and CDK stacks, and handles CDK bootstrap if needed.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="localStack">The LocalStack resource to connect all AWS resources to.</param>
    /// <returns>The distributed application builder.</returns>
    public static IDistributedApplicationBuilder UseLocalStack(this IDistributedApplicationBuilder builder, IResourceBuilder<LocalStackResource> localStack)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(localStack);

        if (!localStack.Resource.Options.UseLocalStack)
        {
            return builder;
        }

        // Check if we have any CDK stacks (IStackResource) - if so, we need CDK bootstrap
        var hasStackResources = builder.Resources
            .OfType<IStackResource>()
            .Any();

        IResourceBuilder<ICloudFormationTemplateResource>? cdkBootstrap = null;

        if (hasStackResources)
        {
            // Create a CDK bootstrap resource once for all CDK stacks
            var bootstrapTemplate = CdkBootstrapManager.GetBootstrapTemplatePath();
            cdkBootstrap = builder.AddAWSCloudFormationTemplate($"CDKBootstrap-{localStack.Resource.Name}", bootstrapTemplate);
        }

        foreach (var resource in builder.Resources.OfType<ICloudFormationTemplateResource>())
        {
            var awsResourceBuilder = builder.CreateResourceBuilder(resource);
            awsResourceBuilder.WaitFor(localStack).WithLocalStack(localStack);

            if (resource is IStackResource && cdkBootstrap != null)
            {
                awsResourceBuilder.WaitFor(cdkBootstrap);
            }
        }

        return builder;
    }
}
