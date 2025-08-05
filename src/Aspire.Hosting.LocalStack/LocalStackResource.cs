#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using LocalStack.Client.Contracts;

namespace Aspire.Hosting.ApplicationModel;

public interface ILocalStackResource : IResourceWithWaitSupport, IResourceWithConnectionString, IResourceWithEnvironment
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
    /// The well-known endpoint name for the LocalStack
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
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"{(Options.Config.UseSsl ? "https://" : "http://")}{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
}
