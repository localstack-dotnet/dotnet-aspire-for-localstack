#pragma warning disable IDE0130, MA0045
// ReSharper disable CheckNamespace

using System.Reflection;
using System.Security.Cryptography;
using static System.Convert;

namespace Aspire.Hosting.LocalStack.CDK;

/// <summary>
/// Manages the extraction and caching of the CDK bootstrap template from an embedded resource.
/// This class is designed to be safe for concurrent, multiprocess execution.
/// </summary>
internal static class CdkBootstrapManager
{
    private const string ResourceName = "Aspire.Hosting.LocalStack.CDK.cdk-bootstrap.template";

    internal static readonly string TempDirPath = Path.Combine(Path.GetTempPath(), "aspire-localstack");

    /// <summary>
    /// Gets the file path of the CDK bootstrap template
    /// </summary>
    /// <returns>The full path to the valid template file on disk.</returns>
    public static string GetBootstrapTemplatePath() => ExtractBootstrapTemplate();

    private static string ExtractBootstrapTemplate()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                             ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");

        using var ms = new MemoryStream();
        resource.CopyTo(ms);
        var content = ms.ToArray();
        var hash = SHA256.HashData(content);
        var hashShort = ToHexString(hash)[..16].ToLowerInvariant();

        var filePath = Path.Combine(TempDirPath, $"cdk-bootstrap-{hashShort}.template");

        Directory.CreateDirectory(TempDirPath);

        if (File.Exists(filePath) && IsFileContentValid(filePath, hash))
        {
            return filePath;
        }

        WriteFileContent(filePath, content, hash, hashShort);

        return filePath;
    }

    private static bool IsFileContentValid(string filePath, byte[] expectedHash)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return SHA256.HashData(fs).AsSpan().SequenceEqual(expectedHash);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the template content to the specified path, handling cross-process concurrency.
    /// This method uses an atomic write-and-rename pattern to ensure file integrity.
    /// </summary>
    private static void WriteFileContent(string filePath, byte[] content, byte[] expectedHash, string hashShort)
    {
        var mutexName = $@"Global\AspireLocalStack-{hashShort}";
        using var mutex = new Mutex(initiallyOwned: false, mutexName);
        var lockTaken = false;

        try
        {
            try
            {
                lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(10));
                if (!lockTaken)
                {
                    throw new TimeoutException($"Could not acquire mutex '{mutexName}' after 10 seconds.");
                }
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            if (!lockTaken)
            {
                return;
            }

            if (File.Exists(filePath) && IsFileContentValid(filePath, expectedHash))
            {
                return;
            }

            var tempFilePath = filePath + ".tmp";
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fileStream.Write(content);
                fileStream.Flush();
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
