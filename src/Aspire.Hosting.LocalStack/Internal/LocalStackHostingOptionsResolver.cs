using Aspire.Hosting.AWS;
using LocalStack.Client.Contracts;
using LocalStack.Client.Options;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting.LocalStack.Internal;

/// <summary>
/// Resolves <see cref="LocalStackHostingOptions"/> from the active configuration, an optional
/// callback, an optional legacy <see cref="ILocalStackOptions"/>, and an optional
/// <see cref="IAWSSDKConfig"/> into an immutable <see cref="LocalStackHostingState"/>. The
/// resolver applies the precedence required by the LocalStack.Hosting contract: defaults,
/// the legacy <c>LocalStack</c> section, the canonical <c>Aspire:Hosting:LocalStack</c>
/// section, the <c>configureOptions</c> callback, and finally <c>IAWSSDKConfig.Region</c>.
/// </summary>
internal static class LocalStackHostingOptionsResolver
{
    /// <summary>Canonical .NET Aspire LocalStack hosting configuration section name.</summary>
    internal const string CanonicalSectionName = "Aspire:Hosting:LocalStack";

    /// <summary>
    /// Legacy <c>LocalStack.Client</c>-owned section name preserved for backwards-compatible
    /// application configuration.
    /// </summary>
    internal const string LegacySectionName = "LocalStack";

    /// <summary>
    /// Resolves hosting state. Returns <see langword="null"/> when LocalStack is disabled in
    /// the resolved options; in that case validation is skipped entirely.
    /// </summary>
    /// <param name="configuration">Application configuration. Never mutated.</param>
    /// <param name="awsConfig">Optional AWS SDK configuration whose <see cref="IAWSSDKConfig.Region"/>
    /// overrides the resolved <see cref="LocalStackHostingOptions.Region"/>.</param>
    /// <param name="localStackOptions">
    /// When non-null, an explicit legacy <see cref="ILocalStackOptions"/> instance that bypasses
    /// section binding entirely. <see cref="IAWSSDKConfig.Region"/> is still applied.
    /// </param>
    /// <param name="configureOptions">Optional callback invoked after section binding; overrides
    /// canonical values but is itself overridden by <see cref="IAWSSDKConfig.Region"/>.</param>
    /// <returns>The frozen <see cref="LocalStackHostingState"/>, or <see langword="null"/> when disabled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <exception cref="DistributedApplicationException">
    /// Thrown when LocalStack is enabled but one or more required properties are missing or
    /// whitespace. The exception message names each invalid property but never includes the
    /// supplied credential values.
    /// </exception>
    public static LocalStackHostingState? Resolve(
        IConfiguration configuration,
        IAWSSDKConfig? awsConfig = null,
        ILocalStackOptions? localStackOptions = null,
        Action<LocalStackHostingOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Explicit legacy ILocalStackOptions bypasses section binding but still receives the
        // IAWSSDKConfig.Region override.
        if (localStackOptions is not null)
        {
            return BuildStateFromExplicitLegacyOptions(localStackOptions, awsConfig);
        }

        var options = new LocalStackHostingOptions();
        // LocalStack.Client's defaults for these compatibility flags are false; preserve
        // them when the legacy section is absent.
        var legacyUseSsl = false;
        var legacyUseLegacyPorts = false;

        // Step 1: translate legacy LocalStack.Client-owned section (if present).
        var legacySection = configuration.GetSection(LegacySectionName);
        if (legacySection.Exists())
        {
            var legacy = new LocalStackOptions();
            legacySection.Bind(legacy, binder => binder.BindNonPublicProperties = true);

            if (legacy.UseLocalStack)
            {
                options.Enabled = true;
            }

            options.Region = legacy.Session.RegionName;
            options.AccessKeyId = legacy.Session.AwsAccessKeyId;
            options.SecretAccessKey = legacy.Session.AwsAccessKey;
            options.SessionToken = legacy.Session.AwsSessionToken;
            legacyUseSsl = legacy.Config.UseSsl;
            legacyUseLegacyPorts = legacy.Config.UseLegacyPorts;
        }

        // Step 2: bind the canonical section over the legacy values (overrides equivalents).
        var canonicalSection = configuration.GetSection(CanonicalSectionName);
        if (canonicalSection.Exists())
        {
            canonicalSection.Bind(options);
        }

        // Step 3: callback overrides canonical values.
        configureOptions?.Invoke(options);

        // Step 4: IAWSSDKConfig.Region overrides the callback region.
        if (awsConfig is { Region: not null })
        {
            options.Region = awsConfig.Region.SystemName;
        }

        if (!options.Enabled)
        {
            // Disabled: return no state and skip validation entirely, even when other
            // properties would otherwise be invalid.
            return null;
        }

        ValidateEnabled(options);

        return new LocalStackHostingState
        {
            Enabled = options.Enabled,
            Region = options.Region,
            AccessKeyId = options.AccessKeyId,
            SecretAccessKey = options.SecretAccessKey,
            SessionToken = options.SessionToken,
            UseSsl = legacyUseSsl,
            UseLegacyPorts = legacyUseLegacyPorts,
        };
    }

    private static LocalStackHostingState? BuildStateFromExplicitLegacyOptions(
        ILocalStackOptions legacy,
        IAWSSDKConfig? awsConfig)
    {
        if (!legacy.UseLocalStack)
        {
            return null;
        }

        var region = awsConfig is { Region: not null }
            ? awsConfig.Region.SystemName
            : legacy.Session.RegionName;

        var options = new LocalStackHostingOptions
        {
            Enabled = true,
            Region = region,
            AccessKeyId = legacy.Session.AwsAccessKeyId,
            SecretAccessKey = legacy.Session.AwsAccessKey,
            SessionToken = legacy.Session.AwsSessionToken,
        };

        ValidateEnabled(options);

        return new LocalStackHostingState
        {
            Enabled = options.Enabled,
            Region = options.Region,
            AccessKeyId = options.AccessKeyId,
            SecretAccessKey = options.SecretAccessKey,
            SessionToken = options.SessionToken,
            UseSsl = legacy.Config.UseSsl,
            UseLegacyPorts = legacy.Config.UseLegacyPorts,
        };
    }

    private static void ValidateEnabled(LocalStackHostingOptions options)
    {
        var invalid = new List<string>(4);

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            invalid.Add(nameof(options.Region));
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            invalid.Add(nameof(options.AccessKeyId));
        }

        if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            invalid.Add(nameof(options.SecretAccessKey));
        }

        if (string.IsNullOrWhiteSpace(options.SessionToken))
        {
            invalid.Add(nameof(options.SessionToken));
        }

        if (invalid.Count is 0)
        {
            return;
        }

        // Property names only; never include the supplied credential values.
        var names = string.Join(", ", invalid);
        throw new DistributedApplicationException(
            $"LocalStack hosting configuration is enabled but the following properties are missing or empty: {names}.");
    }
}
