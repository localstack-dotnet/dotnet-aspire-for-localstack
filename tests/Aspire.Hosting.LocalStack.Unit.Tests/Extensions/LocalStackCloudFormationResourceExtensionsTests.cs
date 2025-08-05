namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions;

public class LocalStackCloudFormationResourceExtensionsTests
{
    [Fact]
    public void WithReference_Should_Configure_CloudFormation_Client_For_LocalStack()
    {
        const string cfResourceName = "test-cf";

        var (app, cfResource) = TestApplicationBuilder.CreateWithResource<ICloudFormationTemplateResource>(cfResourceName, builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        cfResource.ShouldHaveLocalStackEnabledAnnotation();
        cfResource.ShouldWaitFor(localStackResource);
    }

    [Fact]
    public void WithReference_Should_Add_LocalStack_Enabled_Annotation()
    {
        const string cfResourceName = "test-cf";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        var cfResource = app.GetResource<ICloudFormationTemplateResource>(cfResourceName);
        var localStackResource = app.GetResource<ILocalStackResource>("localstack");

        cfResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);
        cfResource.ShouldWaitFor(localStackResource);
    }

    [Fact]
    public void WithReference_Should_Establish_Bidirectional_Reference()
    {
        const string cfResourceName = "test-cf";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        var cfResource = app.GetResource<ICloudFormationTemplateResource>(cfResourceName);
        var localStackResource = app.GetResource<ILocalStackResource>("localstack");

        // CloudFormation should have LocalStack annotation
        cfResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);

        // LocalStack should have reference to CloudFormation resource
        localStackResource.ShouldHaveReferenceToResource(cfResource);
    }

    [Fact]
    public void WithReference_Should_Add_Wait_Dependency_On_LocalStack()
    {
        const string cfResourceName = "test-cf";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        var cfResource = app.GetResource<ICloudFormationTemplateResource>(cfResourceName);
        var localStackResource = app.GetResource<ILocalStackResource>("localstack");

        cfResource.ShouldWaitFor(localStackResource);
    }

    [Fact]
    public void WithReference_Should_Return_Builder_When_LocalStack_Is_Null()
    {
        const string cfResourceName = "test-cf";

        var (app, cfResource) = TestApplicationBuilder.CreateWithResource<ICloudFormationTemplateResource>(cfResourceName, builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var cfBuilder = builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml").WithReference(awsConfig);

            cfBuilder.WithReference(localStackBuilder: null);
        });

        Assert.False(app.HasResource<ILocalStackResource>("localstack"));
        cfResource.ShouldNotHaveLocalStackEnabledAnnotation();
    }

    [Fact]
    public void WithReference_Should_Return_Builder_When_LocalStack_Is_Disabled()
    {
        const string cfResourceName = "test-cf";

        var (app, cfResource) = TestApplicationBuilder.CreateWithResource<ICloudFormationTemplateResource>(cfResourceName, builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (disabledOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);
            var localStack = builder.AddLocalStack(localStackOptions: disabledOptions);

            builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        Assert.False(app.HasResource<ILocalStackResource>("localstack"));
        cfResource.ShouldNotHaveLocalStackEnabledAnnotation();
    }

    [Fact]
    public void WithReference_Should_Throw_ArgumentNullException_When_Builder_Is_Null()
    {
        IResourceBuilder<ICloudFormationTemplateResource> builder = null!;
        var localStack = Substitute.For<IResourceBuilder<ILocalStackResource>>();

        Assert.Throws<ArgumentNullException>(() => builder.WithReference(localStack));
    }

    [Fact]
    public void WithReference_Should_Not_Duplicate_References_When_Called_Multiple_Times()
    {
        const string cfResourceName = "test-cf";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            var cfBuilder = builder.AddAWSCloudFormationTemplate(cfResourceName, "template.yaml")
                .WithReference(awsConfig);

            cfBuilder.WithReference(localStack);
            cfBuilder.WithReference(localStack);
            cfBuilder.WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var referenceAnnotations = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .Where(a => string.Equals(a.Resource.Name, cfResourceName, StringComparison.Ordinal))
            .ToList();

        Assert.Single(referenceAnnotations);
    }

    [Fact]
    public void WithReference_Should_Work_With_Multiple_CloudFormation_Resources()
    {
        using var app = TestApplicationBuilder.Create(builder =>
        {
            var awsConfig = builder.AddAWSSDKConfig().WithRegion(Amazon.RegionEndpoint.USEast1);
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);

            builder.AddAWSCloudFormationTemplate("cf-1", "template1.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);

            builder.AddAWSCloudFormationTemplate("cf-2", "template2.yaml")
                .WithReference(awsConfig)
                .WithReference(localStack);
        });

        var cf1Resource = app.GetResource<ICloudFormationTemplateResource>("cf-1");
        var cf2Resource = app.GetResource<ICloudFormationTemplateResource>("cf-2");
        var localStackResource = app.GetResource<ILocalStackResource>("localstack");

        cf1Resource.ShouldHaveLocalStackEnabledAnnotation();
        cf2Resource.ShouldHaveLocalStackEnabledAnnotation();

        cf1Resource.ShouldWaitFor(localStackResource);
        cf1Resource.ShouldWaitFor(localStackResource);

        localStackResource.ShouldHaveReferenceToResource(cf1Resource);
        localStackResource.ShouldHaveReferenceToResource(cf2Resource);
    }
}
