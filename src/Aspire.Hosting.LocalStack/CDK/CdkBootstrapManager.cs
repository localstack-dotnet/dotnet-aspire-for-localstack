#pragma warning disable IDE0130, MA0045
// ReSharper disable CheckNamespace

using System.Reflection;

namespace Aspire.Hosting.LocalStack.CDK;

/// <summary>
/// Manages CDK bootstrap resources for LocalStack environments.
/// </summary>
internal static class CdkBootstrapManager
{
    /// <summary>
    /// Gets the path to the CDK bootstrap template, extracting it from embedded resources if needed.
    /// The template is extracted to a temporary directory and cached for subsequent calls.
    /// This template contains the AWS CDK bootstrap CloudFormation stack for LocalStack environments.
    /// </summary>
    /// <returns>The absolute path to the CDK bootstrap template file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the embedded CDK bootstrap template cannot be found.</exception>
    public static string GetBootstrapTemplatePath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aspire-localstack", "cdk-bootstrap.template");

        var directory = Path.GetDirectoryName(tempPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("Aspire.Hosting.LocalStack.CDK.cdk-bootstrap.template")
                           ?? throw new InvalidOperationException("Could not find embedded CDK bootstrap template.");

        using var fileStream = File.Create(tempPath);
        stream.CopyTo(fileStream);

        return tempPath;
    }
}
