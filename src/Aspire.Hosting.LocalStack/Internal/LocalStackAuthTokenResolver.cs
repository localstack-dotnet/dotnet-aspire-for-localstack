using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.LocalStack.Internal;

internal static class LocalStackAuthTokenResolver
{
    internal const string EnvironmentVariableName = "LOCALSTACK_AUTH_TOKEN";

    internal static LocalStackAuthTokenSource? Resolve(LocalStackContainerOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        if (options.AuthTokenSource is { } explicitSource)
        {
            return explicitSource;
        }

        if (options.AdditionalEnvironmentVariables.TryGetValue(EnvironmentVariableName, out var token))
        {
            return LocalStackAuthTokenSource.FromLiteral(token);
        }

        var configuredToken = configuration[EnvironmentVariableName];
        return configuredToken is null ? null : LocalStackAuthTokenSource.FromLiteral(configuredToken);
    }
}
