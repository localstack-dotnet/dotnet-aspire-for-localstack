#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using LocalStack.Client.Contracts;

namespace Aspire.Hosting.ApplicationModel;

public interface ILocalStackResource : IResourceWithWaitSupport, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the LocalStack configuration options.
    /// </summary>
    ILocalStackOptions Options { get; }
}

/// <summary>
/// A resource that represents a LocalStack container.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="options">The LocalStack configuration options.</param>
public sealed class LocalStackResource(string name, ILocalStackOptions options) : ContainerResource(name), ILocalStackResource
{
    /// <summary>
    /// The well-known endpoint name for the LocalStack edge port.
    /// </summary>
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the LocalStack edge port.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the LocalStack configuration options.
    /// </summary>
    public ILocalStackOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Gets the connection string expression for the LocalStack resource.
    /// This provides the connection information in the format expected by LocalStack.Client.
    /// Uses the configured LocalStack host and port for consistency with LocalStack.Client configuration.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
        => ReferenceExpression.Create($"http{(Options.Config.UseSsl ? "s" : string.Empty)}://{Options.Config.LocalStackHost}:{Options.Config.EdgePort.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
}
