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
    /// </summary>
    /// <returns>The absolute path to the CDK bootstrap template file.</returns>
    public static string GetBootstrapTemplatePath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "aspire-localstack", "cdk-bootstrap.template");

        if (File.Exists(tempPath))
        {
            return tempPath;
        }
        var directory = Path.GetDirectoryName(tempPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Aspire.Hosting.LocalStack.Templates.cdk-bootstrap.template");

        if (stream == null)
        {
            throw new InvalidOperationException("Could not find embedded CDK bootstrap template.");
        }

        using var fileStream = File.Create(tempPath);
        stream.CopyTo(fileStream);

        return tempPath;
    }
}
