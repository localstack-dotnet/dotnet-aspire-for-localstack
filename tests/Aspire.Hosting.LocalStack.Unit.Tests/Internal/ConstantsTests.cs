using Aspire.Hosting.AWS.Utils.Internal;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class ConstantsTests
{
    public ConstantsTests()
    {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
#pragma warning disable S1481
        IProcessCommandService? commandService = null;
#pragma warning restore S1481
#pragma warning restore CS0219 // Variable is assigned but its value is never used
    }

    [Fact]
    public void DefaultContainerPort_Should_Be_4566()
    {
        Assert.Equal(4566, Constants.DefaultContainerPort);
    }

    [Fact]
    public void CloudFormationReferenceAnnotation_Should_Have_Correct_Type_Name()
    {
        Assert.Equal("Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation", Constants.CloudFormationReferenceAnnotation);
    }

    [Fact]
    public void SQSEventSourceResource_Should_Have_Correct_Type_Name()
    {
        Assert.Equal("Aspire.Hosting.AWS.Lambda.SQSEventSourceResource", Constants.SQSEventSourceResource);
    }

    [Fact]
    public void CloudFormationReferenceAnnotation_Type_Should_Exist_In_AWS_Assembly()
    {
        // Act & Assert
        var type = GetTypeByName(Constants.CloudFormationReferenceAnnotation);
        Assert.NotNull(type);
        Assert.Equal(Constants.CloudFormationReferenceAnnotation, type.FullName);

        // Verify it's an annotation type
        Assert.True(typeof(IResourceAnnotation).IsAssignableFrom(type),
            $"Type {Constants.CloudFormationReferenceAnnotation} should implement IResourceAnnotation");
    }

    [Fact]
    public void SQSEventSourceResource_Type_Should_Exist_In_AWS_Assembly()
    {
        // Act & Assert
        var type = GetTypeByName(Constants.SQSEventSourceResource);
        Assert.NotNull(type);
        Assert.Equal(Constants.SQSEventSourceResource, type.FullName);

        // Verify it's an executable resource type
        Assert.True(typeof(ExecutableResource).IsAssignableFrom(type),
            $"Type {Constants.SQSEventSourceResource} should inherit from ExecutableResource");
    }

    [Theory]
    [InlineData("Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation")]
    [InlineData("Aspire.Hosting.AWS.Lambda.SQSEventSourceResource")]
    public void AWS_Types_Should_Be_Accessible_From_Current_Assembly_Context(string typeName)
    {
        // This test ensures we can find AWS types at runtime
        // Important for catching assembly loading or reference issues
        var type = GetTypeByName(typeName);
        Assert.NotNull(type);
        Assert.Equal(typeName, type.FullName);
    }

    [Fact]
    public void AWS_Assembly_Dependencies_Should_Be_Available()
    {
        // Verify we can access the AWS assemblies our constants reference
        var awsAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.Contains("Aspire.Hosting.AWS", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(awsAssemblies);

        // Log available AWS assemblies for debugging
        var assemblyNames = string.Join(", ", awsAssemblies.Select(a => a.GetName().Name));
        Assert.True(awsAssemblies.Count > 0, $"Available AWS assemblies: {assemblyNames}");
    }

    private static Type? GetTypeByName(string typeName)
    {
        // Try to find the type across all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }
            catch (Exception ex) when (ex is System.Reflection.ReflectionTypeLoadException or FileNotFoundException)
            {
                // Assembly might not be loaded or accessible, continue searching
                continue;
            }
        }

        // If not found in loaded assemblies, try Type.GetType which handles assembly-qualified names
        try
        {
            return Type.GetType(typeName);
        }
        catch (Exception ex) when (ex is TypeLoadException or ArgumentException or FileNotFoundException)
        {
            return null;
        }
    }
}
