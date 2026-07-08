namespace Aspire.Hosting.LocalStack.Unit.Tests.TestUtilities;

/// <summary>
/// Creates Aspire.Hosting.AWS helper resources (e.g., DynamoDB Streams event sources)
/// by type name. Reflection-based discovery mirrors ConstantsTests: type discovery must
/// not depend on whether another test already forced Aspire.Hosting.AWS into the AppDomain.
/// </summary>
internal static class TestResourceFactory
{
    public static ExecutableResource CreateExecutableResourceByTypeName(string typeName, string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                       .FirstOrDefault(t => t is not null)
                   ?? System.Reflection.Assembly.Load("Aspire.Hosting.AWS").GetType(typeName, throwOnError: false)
                   ?? throw new InvalidOperationException($"Type '{typeName}' was not found in the current assembly context.");

        return (ExecutableResource)(Activator.CreateInstance(
                                        type,
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                        binder: null,
                                        args: [name],
                                        culture: null)
                                    ?? throw new InvalidOperationException($"Type '{typeName}' could not be created."));
    }
}
