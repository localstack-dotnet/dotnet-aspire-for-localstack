#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Aspire.Hosting.LocalStack.Internal;
using LocalStack.Client.Contracts;
using LocalStack.Client.Options;

namespace Aspire.Hosting.ApplicationModel;

public interface ILocalStackResource : IResourceWithWaitSupport, IResourceWithConnectionString, IResourceWithEnvironment
{
    /// <summary>
    /// Gets the LocalStack configuration options.
    /// </summary>
#pragma warning disable S1133
    [Obsolete("Use LocalStackResource.HostingState through package-owned runtime adapters instead. This property will be removed in the next major version.", false)]
#pragma warning restore S1133
    public ILocalStackOptions Options { get; }
}

internal interface ILocalStackHostingStateProvider
{
    public LocalStackHostingState HostingState { get; }
}

/// <summary>
/// A resource that represents a LocalStack container.
/// </summary>
public sealed class LocalStackResource : ContainerResource, ILocalStackResource, ILocalStackHostingStateProvider
{
    /// <summary>
    /// The well-known endpoint name for the LocalStack
    /// </summary>
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;
    private ILocalStackOptions? _options;

    internal LocalStackResource(string name, LocalStackHostingState hostingState)
        : base(name)
    {
        HostingState = hostingState ?? throw new ArgumentNullException(nameof(hostingState));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackResource"/> class from LocalStack.Client options.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The LocalStack configuration options.</param>
#pragma warning disable S1133
    [Obsolete("Use LocalStackResource(string name, LocalStackHostingState hostingState) through AddLocalStack package-owned overloads instead. This constructor will be removed in the next major version.", false)]
#pragma warning restore S1133
    public LocalStackResource(string name, ILocalStackOptions options)
        : this(name, CreateHostingState(options))
    {
    }

    internal LocalStackHostingState HostingState { get; }

    LocalStackHostingState ILocalStackHostingStateProvider.HostingState => HostingState;

    /// <summary>
    /// Gets the primary endpoint for the LocalStack edge port.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the LocalStack configuration options.
    /// </summary>
#pragma warning disable S1133
    [Obsolete("Use LocalStackResource.HostingState through package-owned runtime adapters instead. This property will be removed in the next major version.", false)]
#pragma warning restore S1133
    public ILocalStackOptions Options => _options ??= CreateLocalStackOptions(HostingState);

    /// <summary>
    /// Gets the connection string expression for the LocalStack resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"{(HostingState.UseSsl ? "https://" : "http://")}{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    private static LocalStackHostingState CreateHostingState(ILocalStackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new LocalStackHostingState
        {
            Enabled = options.UseLocalStack,
            Region = options.Session.RegionName,
            AccessKeyId = options.Session.AwsAccessKeyId,
            SecretAccessKey = options.Session.AwsAccessKey,
            SessionToken = options.Session.AwsSessionToken,
            UseSsl = options.Config.UseSsl,
            UseLegacyPorts = options.Config.UseLegacyPorts,
        };
    }

    private static LocalStackOptions CreateLocalStackOptions(LocalStackHostingState state)
        => new(
            state.Enabled,
            new SessionOptions(
                state.AccessKeyId,
                state.SecretAccessKey,
                state.SessionToken,
                state.Region),
            new ConfigOptions(useSsl: state.UseSsl, useLegacyPorts: state.UseLegacyPorts));
}
