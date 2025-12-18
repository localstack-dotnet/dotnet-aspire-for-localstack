namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions.ResourceBuilderExtensionsTests;

public class UseLocalStackTests
{
    [Test]
    public async Task UseLocalStack_Should_Return_Builder_When_LocalStack_Is_Null()
    {
        using var app = TestApplicationBuilder.Create(builder => builder.UseLocalStack(localStack: null));

        await app.ShouldHaveResourceCount<ILocalStackResource>(0);
    }

    [Test]
    public async Task UseLocalStack_Should_Return_Builder_When_LocalStack_Is_Disabled()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (disabledOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);
            var localStack = builder.AddLocalStack(localStackOptions: disabledOptions);
            builder.UseLocalStack(localStack);
        });

        await app.ShouldHaveResourceCount<ILocalStackResource>(0);
    }

    [Test]
    public async Task UseLocalStack_Should_Configure_CloudFormation_Resources_With_LocalStack_Reference()
    {
        var (app, cfResource) = TestApplicationBuilder.CreateWithResource<ICloudFormationTemplateResource>("test-cf", builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            // Add CloudFormation template BEFORE calling UseLocalStack
            builder.AddAWSCloudFormationTemplate("test-cf", "template.yaml")
                .WithReference(awsConfig);

            // This should auto-configure the CloudFormation resource
            builder.UseLocalStack(localStack);
        });

        await cfResource.ShouldHaveLocalStackEnabledAnnotation();
        await app.ShouldHaveResourceCount<ILocalStackResource>(1);
    }

    [Test]
    public async Task UseLocalStack_Should_Configure_Multiple_CloudFormation_Resources()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            // Add multiple CloudFormation resources
            builder.AddAWSCloudFormationTemplate("cf-1", "template1.yaml").WithReference(awsConfig);
            builder.AddAWSCloudFormationTemplate("cf-2", "template2.yaml").WithReference(awsConfig);

            // This should auto-configure ALL CloudFormation resources
            builder.UseLocalStack(localStack);
        });

        var cfResources = app.GetResources<ICloudFormationTemplateResource>().ToList();
        await Assert.That(cfResources.Count).IsEqualTo(2);

        foreach (var cfResource in cfResources)
        {
            await cfResource.ShouldHaveLocalStackEnabledAnnotation();
        }
    }

    [Test]
    public async Task UseLocalStack_Should_Configure_Projects_That_Reference_AWS_Resources()
    {
        var (_, projectResource) = TestApplicationBuilder.CreateWithResource<ProjectResource>("test-project", builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            var cfTemplate = builder.AddAWSCloudFormationTemplate("cf-template", "template.yaml")
                .WithReference(awsConfig);

            // Project references AWS resource
            builder.AddProject("test-project", TestDataBuilders.GetTestProjectPath())
                .WithReference(cfTemplate);

            // This should autoconfigure the project with LocalStack environment
            builder.UseLocalStack(localStack);
        });

        await projectResource.ShouldHaveLocalStackEnabledAnnotation();
    }

    [Test]
    public async Task UseLocalStack_Should_Create_CDK_Bootstrap_When_Explicitly_Called()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            // Explicitly create CDK bootstrap (this tests the actual method)
            builder.AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localStack);

            builder.UseLocalStack(localStack);
        });

        var bootstrapResources = app.GetResources<ICloudFormationTemplateResource>()
            .Where(r => string.Equals(r.Name, "CDKBootstrap", StringComparison.Ordinal))
            .ToList();

        await Assert.That(bootstrapResources).HasSingleItem();
        await bootstrapResources[0].ShouldHaveLocalStackEnabledAnnotation();
    }

    [Test]
    public async Task UseLocalStack_Should_Handle_Empty_Application_Gracefully()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            // Call UseLocalStack on an empty application - should not throw
            builder.UseLocalStack(localStack);
        });

        await app.ShouldHaveResourceCount<ILocalStackResource>(1);
    }

    [Test]
    public async Task UseLocalStack_Should_Not_Configure_Resources_Already_Marked_With_LocalStack()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            // Manually configure a CloudFormation resource with LocalStack first
            builder.AddAWSCloudFormationTemplate("manually-configured", "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);

            builder.UseLocalStack(localStack);
        });

        var cfResource = app.GetResource<ICloudFormationTemplateResource>("manually-configured");
        await cfResource.ShouldHaveLocalStackEnabledAnnotation();

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var referenceAnnotations = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .Where(a => string.Equals(a.Resource.Name, "manually-configured", StringComparison.Ordinal))
            .ToList();

        await Assert.That(referenceAnnotations).HasSingleItem();
    }

    [Test]
    public async Task UseLocalStack_Should_Establish_Bidirectional_References()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate("test-resource", "template.yaml")
                .WithReference(awsConfig);

            builder.UseLocalStack(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var cfResource = app.GetResource<ICloudFormationTemplateResource>("test-resource");

        // LocalStack should reference the CloudFormation resource
        await localStackResource.ShouldHaveReferenceToResource(cfResource);

        // CloudFormation resource should be enabled for LocalStack
        await cfResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);
    }
}
