using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Internal;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Internal;

namespace Aspire.Hosting.LocalStack.Internal;

internal sealed class LocalStackCdkAssetUploadEndpointCustomizer(Uri localStackUrl) : IRuntimePipelineCustomizer
{
    internal static string UniqueNameValue => Constants.CdkAssetUploadPipelineCustomizeName;

    public string UniqueName => UniqueNameValue;

    /// <summary>
    /// Registers a fresh customizer for the given LocalStack endpoint, replacing any previous registration
    /// (last-writer-wins; overlapping distinct LocalStack endpoints are not a supported topology).
    /// </summary>
    internal static void Register(Uri localStackUrl)
    {
        ArgumentNullException.ThrowIfNull(localStackUrl);
        RuntimePipelineCustomizerRegistry.Instance.Deregister(UniqueNameValue);
        RuntimePipelineCustomizerRegistry.Instance.Register(new LocalStackCdkAssetUploadEndpointCustomizer(localStackUrl));
    }

    internal static void Deregister() => RuntimePipelineCustomizerRegistry.Instance.Deregister(UniqueNameValue);

    public void Customize(Type type, RuntimePipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(pipeline);

        // The insertion points intentionally depend on the S3/STS endpoint resolver handler types and ordering.
        // If the AWS SDK renames or reorders them, AddHandlerBefore/AddHandlerAfter fails during client
        // construction; the focused unit tests pin the resulting positions and fail first.
        if (type == typeof(AmazonS3Client))
        {
            if (!HasHandler<LocalStackCdkAssetUploadForcePathStyleHandler>(pipeline))
            {
                pipeline.AddHandlerBefore<AmazonS3EndpointResolver>(new LocalStackCdkAssetUploadForcePathStyleHandler());
            }

            if (!HasHandler<LocalStackCdkAssetUploadEndpointRedirectHandler>(pipeline))
            {
                pipeline.AddHandlerAfter<AmazonS3EndpointResolver>(new LocalStackCdkAssetUploadEndpointRedirectHandler(localStackUrl));
            }
        }
        else if (type == typeof(AmazonSecurityTokenServiceClient) && !HasHandler<LocalStackCdkAssetUploadEndpointRedirectHandler>(pipeline))
        {
            pipeline.AddHandlerAfter<AmazonSecurityTokenServiceEndpointResolver>(new LocalStackCdkAssetUploadEndpointRedirectHandler(localStackUrl));
        }
    }

    private static bool HasHandler<THandler>(RuntimePipeline pipeline)
        where THandler : IPipelineHandler
        => pipeline.EnumerateHandlers().Any(static handler => handler is THandler);
}

/// <summary>
/// Forces S3 path-style addressing for the CDK asset uploader's S3 client. Runs before endpoint
/// resolution so the bucket is placed in the request path (which survives the later authority swap).
/// </summary>
internal sealed class LocalStackCdkAssetUploadForcePathStyleHandler : PipelineHandler
{
    public override void InvokeSync(IExecutionContext executionContext)
    {
        EnableForcePathStyle(executionContext);
        base.InvokeSync(executionContext);
    }

    public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        EnableForcePathStyle(executionContext);
        return base.InvokeAsync<T>(executionContext);
    }

    private static void EnableForcePathStyle(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        if (executionContext.RequestContext.ClientConfig is AmazonS3Config config)
        {
            config.ForcePathStyle = true;
        }
    }
}

/// <summary>
/// Rewrites only the authority (scheme/host/port) of the already-resolved request endpoint to the
/// LocalStack endpoint, preserving the resolved path. Runs after endpoint resolution and does not touch
/// the client config, so <see cref="Amazon.Runtime.ClientConfig.RegionEndpoint"/> stays set — the CDK
/// asset uploader reads that region back from its STS client after GetCallerIdentity.
/// </summary>
internal sealed class LocalStackCdkAssetUploadEndpointRedirectHandler(Uri localStackUrl) : PipelineHandler
{
    public override void InvokeSync(IExecutionContext executionContext)
    {
        RedirectEndpoint(executionContext);
        base.InvokeSync(executionContext);
    }

    public override Task<T> InvokeAsync<T>(IExecutionContext executionContext)
    {
        RedirectEndpoint(executionContext);
        return base.InvokeAsync<T>(executionContext);
    }

    private void RedirectEndpoint(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        var request = executionContext.RequestContext.Request;
        if (request?.Endpoint is { } resolved)
        {
            request.Endpoint = new UriBuilder(resolved)
            {
                Scheme = localStackUrl.Scheme,
                Host = localStackUrl.Host,
                Port = localStackUrl.Port,
            }.Uri;
        }
    }
}
