namespace Aspire.Hosting.LocalStack.Unit.Tests.Extensions;

public class LocalStackProjectExtensionsTests
{
    [Test]
    public async Task WithReference_Should_Add_LocalStack_Reference_To_Project()
    {
        const string testProjectResourceName = "test-project";

        var (app, projectResource) = TestApplicationBuilder.CreateWithResource<ProjectResource>(testProjectResourceName, builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);
            builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath())
                .WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        await projectResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);
    }

    [Test]
    public async Task WithReference_Should_Return_Builder_When_LocalStack_Is_Null()
    {
        const string testProjectResourceName = "test-project";

        IResourceBuilder<ProjectResource>? resultBuilder = null;
        IResourceBuilder<ProjectResource>? originalBuilder = null;

        var (app, projectResource) = TestApplicationBuilder.CreateWithResource<ProjectResource>(testProjectResourceName, builder =>
        {
            var projectBuilder = builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath());
            originalBuilder = projectBuilder;
            resultBuilder = projectBuilder.WithReference(localStackBuilder: null);
        });

        // Should return the same builder instance
        await Assert.That(resultBuilder).IsSameReferenceAs(originalBuilder);

        await Assert.That(app.HasResource<ILocalStackResource>("localstack")).IsFalse();
        await projectResource.ShouldNotHaveLocalStackEnabledAnnotation();
    }

    [Test]
    public async Task WithReference_Should_Return_Builder_When_LocalStack_Is_Disabled()
    {
        const string testProjectResourceName = "test-project";

        var (app, cfResource) = TestApplicationBuilder.CreateWithResource<ProjectResource>(testProjectResourceName, builder =>
        {
            var projectBuilder = builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath());
            var (disabledOptions, _, _) = TestDataBuilders.CreateMockLocalStackOptions(useLocalStack: false);
            var localStack = builder.AddLocalStack(localStackOptions: disabledOptions);

            projectBuilder.WithReference(localStackBuilder: localStack);
        });

        await Assert.That(app.HasResource<ILocalStackResource>("localstack")).IsFalse();
        await cfResource.ShouldNotHaveLocalStackEnabledAnnotation();
    }

    [Test]
    public async Task WithReference_Should_Throw_ArgumentNullException_When_Builder_Is_Null()
    {
        IResourceBuilder<ProjectResource> builder = null!;
        var localStack = Substitute.For<IResourceBuilder<ILocalStackResource>>();

        await Assert.That(() => builder.WithReference(localStack)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WithReference_Should_Establish_Bidirectional_Reference()
    {
        const string testProjectResourceName = "test-project";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);
            builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath())
                .WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var projectResource = app.GetResource<ProjectResource>(testProjectResourceName);

        await projectResource.ShouldHaveLocalStackEnabledAnnotation(localStackResource);
        await localStackResource.ShouldHaveReferenceToResource(projectResource);
    }

    [Test]
    public async Task WithReference_Should_Add_Wait_Dependency_On_LocalStack()
    {
        const string testProjectResourceName = "test-project";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);
            builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath())
                .WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var projectResource = app.GetResource<ProjectResource>(testProjectResourceName);

        await projectResource.ShouldWaitFor(localStackResource);
    }

    [Test]
    public async Task WithReference_Should_Configure_LocalStack_Environment_Variables()
    {
        const string testProjectResourceName = "test-project";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions(
                useLocalStack: true,
                regionName: "us-west-1",
                edgePort: 4567);

            var localStack = builder.AddLocalStack(localStackOptions: options);
            builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath())
                .WithReference(localStack);
        });

        var projectResource = app.GetResource<ProjectResource>(testProjectResourceName);
        var localStackResource = app.GetResource<ILocalStackResource>("localstack");

        await projectResource.ShouldHaveLocalStackEnvironmentConfiguration(localStackResource);
    }

    [Test]
    public async Task WithReference_Should_Not_Duplicate_References_When_Called_Multiple_Times()
    {
        const string testProjectResourceName = "test-project";

        using var app = TestApplicationBuilder.Create(builder =>
        {
            var (options, _, _) = TestDataBuilders.CreateMockLocalStackOptions();
            var localStack = builder.AddLocalStack(localStackOptions: options);
            var projectBuilder = builder.AddProject(testProjectResourceName, TestDataBuilders.GetTestProjectPath());

            // Call WithReference multiple times
            projectBuilder.WithReference(localStack);
            projectBuilder.WithReference(localStack);
            projectBuilder.WithReference(localStack);
        });

        var localStackResource = app.GetResource<ILocalStackResource>("localstack");
        var referenceAnnotations = localStackResource.Annotations
            .OfType<LocalStackReferenceAnnotation>()
            .Where(a => string.Equals(a.Resource.Name, testProjectResourceName, StringComparison.Ordinal))
            .ToList();

        await Assert.That(referenceAnnotations).HasSingleItem();
    }
}
