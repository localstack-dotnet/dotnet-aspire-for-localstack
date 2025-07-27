namespace Aspire.Hosting.LocalStack;

/// <summary>
/// Container image tags for LocalStack.
/// </summary>
internal static class LocalStackContainerImageTags
{
    /// <summary>
    /// The registry where LocalStack images are hosted.
    /// </summary>
    internal const string Registry = "docker.io";

    /// <summary>
    /// The LocalStack container image name.
    /// </summary>
    internal const string Image = "localstack/localstack";

    /// <summary>
    /// The default LocalStack container image tag.
    /// </summary>
    internal const string Tag = "4.6.0";
}
