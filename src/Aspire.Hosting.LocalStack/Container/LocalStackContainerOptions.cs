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
    /// <para><see cref="ContainerLifetime.Session"/> (Default - Recommended):</para>
    /// <list type="bullet">
    /// <item><description>Container is cleaned up when application stops</description></item>
    /// <item><description>Uses dynamic port assignment by default (unless <see cref="Port"/> is explicitly set)</description></item>
    /// <item><description>Best for: CI/CD pipelines, integration tests, clean state guarantees</description></item>
    /// </list>
    /// <para><see cref="ContainerLifetime.Persistent"/>:</para>
    /// <list type="bullet">
    /// <item><description>Container survives application restarts</description></item>
    /// <item><description>Uses static port 4566 by default (unless <see cref="Port"/> is explicitly set)</description></item>
    /// <item><description>Best for: Local development with container reuse</description></item>
    /// </list>
    /// </remarks>
    public ContainerLifetime Lifetime { get; set; } = ContainerLifetime.Session;

    /// <summary>
    /// Gets or sets the container registry to pull the LocalStack image from.
    /// </summary>
    /// <remarks>
    /// <para>Defaults to "docker.io" if not specified.</para>
    /// <para>Override this when using a private registry or mirror (e.g., Artifactory, Azure Container Registry).</para>
    /// <para>Examples: "artifactory.company.com", "myregistry.azurecr.io", "ghcr.io"</para>
    /// </remarks>
    public string? ContainerRegistry { get; set; }

    /// <summary>
    /// Gets or sets the LocalStack container image name.
    /// </summary>
    /// <remarks>
    /// <para>Defaults to "localstack/localstack" if not specified.</para>
    /// <para>Override when using a custom image path in your registry.</para>
    /// <para>Examples: "my-team/localstack", "mirrors/localstack/localstack", "docker-local/localstack"</para>
    /// </remarks>
    public string? ContainerImage { get; set; }

    /// <summary>
    /// Gets or sets the LocalStack container image tag/version.
    /// </summary>
    /// <remarks>
    /// <para>Defaults to the version bundled with this package if not specified.</para>
    /// <para>Override to use a specific version.</para>
    /// <para>Examples: "latest", "4.9.2", "4.10.0", "custom-build-123"</para>
    /// </remarks>
    public string? ContainerImageTag { get; set; }

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

    /// <summary>
    /// Gets or sets the port to expose LocalStack on the host machine.
    /// </summary>
    /// <remarks>
    /// <para>Controls the host port mapping for the LocalStack container. Interacts with <see cref="Lifetime"/>:</para>
    /// <list type="bullet">
    /// <item><description><c>Port = null</c> + <c>Lifetime = Session</c> (Default): Uses dynamic port assignment (avoids conflicts)</description></item>
    /// <item><description><c>Port = null</c> + <c>Lifetime = Persistent</c>: Uses static port 4566 (default LocalStack port)</description></item>
    /// <item><description><c>Port = 4566</c> (or any value): Always uses the specified static port (overrides lifetime defaults)</description></item>
    /// </list>
    /// <para>Use static ports for predictable URLs and external tool integration. Use dynamic ports for parallel testing and CI/CD.</para>
    /// </remarks>
    public int? Port { get; set; }
}
