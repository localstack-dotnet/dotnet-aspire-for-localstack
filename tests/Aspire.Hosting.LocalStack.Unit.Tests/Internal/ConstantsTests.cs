using System.Reflection;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public class ConstantsTests
{
    public ConstantsTests()
    {
        // Force load AWS assembly for type validation tests
        // This ensures the assembly is in the AppDomain before we try to find types
        var awsAssembly = System.Reflection.Assembly.Load("Aspire.Hosting.AWS");
        _ = awsAssembly; // Suppress unused warning
    }

    [Test]
    public async Task DefaultContainerPort_Should_Be_4566()
    {
#pragma warning disable TUnitAssertions0005 // These tests intentionally verify constant values
        await Assert.That(Constants.DefaultContainerPort).IsEqualTo(4566);
#pragma warning restore TUnitAssertions0005
    }

    [Test]
    public async Task CloudFormationReferenceAnnotation_Should_Have_Correct_Type_Name()
    {
#pragma warning disable TUnitAssertions0005 // These tests intentionally verify constant values
        await Assert.That(Constants.CloudFormationReferenceAnnotation).IsEqualTo("Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation");
#pragma warning restore TUnitAssertions0005
    }

    [Test]
    public async Task SQSEventSourceResource_Should_Have_Correct_Type_Name()
    {
#pragma warning disable TUnitAssertions0005 // These tests intentionally verify constant values
        await Assert.That(Constants.SQSEventSourceResource).IsEqualTo("Aspire.Hosting.AWS.Lambda.SQSEventSourceResource");
#pragma warning restore TUnitAssertions0005
    }

    [Test]
    public async Task CloudFormationReferenceAnnotation_Type_Should_Exist_In_AWS_Assembly()
    {
        // Act & Assert
        var type = GetTypeByName(Constants.CloudFormationReferenceAnnotation);
        await Assert.That(type).IsNotNull();
        await Assert.That(type!.FullName).IsEqualTo(Constants.CloudFormationReferenceAnnotation);

        // Verify it's an annotation type
        await Assert.That(typeof(IResourceAnnotation).IsAssignableFrom(type)).IsTrue()
            .Because($"Type {Constants.CloudFormationReferenceAnnotation} should implement IResourceAnnotation");
    }

    [Test]
    public async Task SQSEventSourceResource_Type_Should_Exist_In_AWS_Assembly()
    {
        // Act & Assert
        var type = GetTypeByName(Constants.SQSEventSourceResource);
        await Assert.That(type).IsNotNull();
        await Assert.That(type!.FullName).IsEqualTo(Constants.SQSEventSourceResource);

        // Verify it's an executable resource type
        await Assert.That(typeof(ExecutableResource).IsAssignableFrom(type)).IsTrue()
            .Because($"Type {Constants.SQSEventSourceResource} should inherit from ExecutableResource");
    }

    [Test]
    [Arguments("Aspire.Hosting.AWS.CloudFormation.CloudFormationReferenceAnnotation")]
    [Arguments("Aspire.Hosting.AWS.Lambda.SQSEventSourceResource")]
    public async Task AWS_Types_Should_Be_Accessible_From_Current_Assembly_Context(string typeName)
    {
        // This test ensures we can find AWS types at runtime
        // Important for catching assembly loading or reference issues
        var type = GetTypeByName(typeName);
        await Assert.That(type).IsNotNull();
        await Assert.That(type!.FullName).IsEqualTo(typeName);
    }

    [Test]
    public async Task AWS_Assembly_Dependencies_Should_Be_Available()
    {
        // Verify we can access the AWS assemblies our constants reference
        var awsAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.Contains("Aspire.Hosting.AWS", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        await Assert.That(awsAssemblies).IsNotEmpty();

        // Log available AWS assemblies for debugging
        var assemblyNames = string.Join(", ", awsAssemblies.Select(a => a.GetName().Name));
        await Assert.That(awsAssemblies.Count > 0).IsTrue()
            .Because($"Available AWS assemblies: {assemblyNames}");
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
            catch (Exception ex) when (ex is ReflectionTypeLoadException or FileNotFoundException)
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
