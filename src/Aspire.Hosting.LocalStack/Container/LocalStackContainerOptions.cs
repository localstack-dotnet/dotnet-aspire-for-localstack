#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.LocalStack.Container;
using LocalStack.Client.Enums;

namespace Aspire.Hosting.LocalStack;

/// <summary>
/// Configuration options for LocalStack container behavior in Aspire.
/// </summary>
public sealed class LocalStackContainerOptions
{
    /// <summary>
    /// Gets or sets the container lifetime behavior.
    /// </summary>
    /// <remarks>
    /// - <see cref="ContainerLifetime.Persistent"/>: Container survives application restarts (default for databases)
    /// - <see cref="ContainerLifetime.Session"/>: Container is cleaned up when application stops (recommended for LocalStack)
    /// </remarks>
    public ContainerLifetime Lifetime { get; set; } = ContainerLifetime.Persistent;

    /// <summary>
    /// Gets or sets the DEBUG environment variable value for LocalStack.
    /// </summary>
    /// <remarks>
    /// 0 = Default log level (default)
    /// 1 = Increased log level with more verbose logs (useful for troubleshooting)
    /// </remarks>
    public int DebugLevel { get; set; }

    /// <summary>
    /// Gets or sets the LS_LOG environment variable value for LocalStack.
    /// </summary>
    /// <remarks>
    /// Controls the LocalStack log level. Currently overrides the DEBUG configuration.
    /// - trace: Detailed request/response logging
    /// - trace-internal: Internal calls logging
    /// - debug: Debug level logging
    /// - info: Info level logging (default)
    /// - warn: Warning level logging
    /// - error: Error level logging
    /// - warning: Warning level logging (alias for warning)
    /// </remarks>
    public LocalStackLogLevel LogLevel { get; set; } = LocalStackLogLevel.Error;

    /// <summary>
    /// Gets or sets additional environment variables to pass to the LocalStack container.
    /// </summary>
    public IDictionary<string, string> AdditionalEnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>A collection of services to eagerly start.</summary>
    public IReadOnlyCollection<AwsService> EagerLoadedServices { get; set; } = [];

    /// <summary>
    /// Gets or sets whether to mount the Docker socket to enable container-based features like Lambda.
    /// </summary>
    /// <remarks>
    /// When enabled, mounts /var/run/docker.sock to allow LocalStack to create containers.
    /// Required for LocalStack Lambda support. Has security implications - use only when needed.
    /// Default: false
    /// </remarks>
    public bool EnableDockerSocket { get; set; }
}
