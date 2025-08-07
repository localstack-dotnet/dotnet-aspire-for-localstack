using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Collection definition for CDK-related tests to ensure they run sequentially.
/// This prevents JSII runtime concurrency issues when multiple tests try to initialize CDK simultaneously.
/// </summary>
[CollectionDefinition("CdkSequential", DisableParallelization = true)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
[SuppressMessage("Design", "MA0048:File name must match type name")]
public class CdkTestCollection : ICollectionFixture<CdkTestFixture>
{
}

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal")]
[SuppressMessage("Design", "CA1063:Implement IDisposable Correctly")]
[SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize")]
[SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly")]
public class CdkTestFixture : IDisposable
{
    public void Dispose()
    {
    }
}
