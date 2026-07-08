using OpenTelemetry.Trace;

namespace LocalStack.Lambda.Frontend;

internal static class CommandCenterRefreshTelemetry
{
    private const string TraceRefreshHeaderName = "X-Command-Center-Trace-Refresh";

    private static readonly AsyncLocal<bool> SuppressRefreshTelemetry = new();

    public static bool IsSuppressed => SuppressRefreshTelemetry.Value;

    public static bool ShouldSuppressServerSpan(HttpRequest request) => IsRefreshEndpoint(request.Path) && !ShouldTraceRefresh(request);

    public static IDisposable SuppressIfRefreshTracingDisabled(HttpRequest request) =>
        ShouldTraceRefresh(request) ? NullScope.Instance : new SuppressionScope();

    private static bool IsRefreshEndpoint(PathString path) =>
        path.StartsWithSegments("/api/config", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/snapshot", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/links", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api/analytics", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldTraceRefresh(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(TraceRefreshHeaderName, out var values))
        {
            return false;
        }

        return bool.TryParse(values.FirstOrDefault(), out var traceRefresh) && traceRefresh;
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly bool _previousValue = SuppressRefreshTelemetry.Value;

        public SuppressionScope()
        {
            SuppressRefreshTelemetry.Value = true;
        }

        public void Dispose() => SuppressRefreshTelemetry.Value = _previousValue;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

internal sealed class CommandCenterRefreshSampler : Sampler
{
    private static readonly Sampler DefaultSampler = new ParentBasedSampler(new AlwaysOnSampler());
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        return CommandCenterRefreshTelemetry.IsSuppressed
            ? new SamplingResult(SamplingDecision.Drop)
            : DefaultSampler.ShouldSample(samplingParameters);
    }
}
