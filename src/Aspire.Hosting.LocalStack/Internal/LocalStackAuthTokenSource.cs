using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.LocalStack.Internal;

internal enum LocalStackAuthTokenSourceKind
{
    Literal,
    Parameter,
}

internal sealed class LocalStackAuthTokenSource
{
    private LocalStackAuthTokenSource(
        LocalStackAuthTokenSourceKind kind,
        string? literal,
        IResourceBuilder<ParameterResource>? parameter)
    {
        Kind = kind;
        Literal = literal;
        Parameter = parameter;
    }

    internal LocalStackAuthTokenSourceKind Kind { get; }

    internal string? Literal { get; }

    internal IResourceBuilder<ParameterResource>? Parameter { get; }

    public override string ToString() => nameof(LocalStackAuthTokenSource);

    internal static LocalStackAuthTokenSource FromLiteral(string authToken)
    {
        ArgumentNullException.ThrowIfNull(authToken);

        return new LocalStackAuthTokenSource(LocalStackAuthTokenSourceKind.Literal, authToken, null);
    }

    internal static LocalStackAuthTokenSource FromParameter(IResourceBuilder<ParameterResource> authToken)
    {
        ArgumentNullException.ThrowIfNull(authToken);
        if (!authToken.Resource.Secret)
        {
            throw new ArgumentException("The activation parameter must be secret.", nameof(authToken));
        }

        return new LocalStackAuthTokenSource(LocalStackAuthTokenSourceKind.Parameter, null, authToken);
    }
}
