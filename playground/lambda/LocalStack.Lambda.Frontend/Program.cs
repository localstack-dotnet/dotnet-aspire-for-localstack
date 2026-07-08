using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LocalStack.Client.Extensions;
using LocalStack.Lambda.Frontend;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;

const string apiGatewayHttpClientName = "ApiGateway";
const int maxFeedItems = 25;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(options => options.ConfigureTracingBeforeDefaults(static tracing => tracing.SetSampler(new CommandCenterRefreshSampler())));

builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
{
    var previousFilter = options.Filter;

    options.Filter = httpContext =>
        !CommandCenterRefreshTelemetry.ShouldSuppressServerSpan(httpContext.Request)
        && (previousFilter?.Invoke(httpContext) ?? true);
});

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonDynamoDB>();

var apiGatewayBaseUrl = builder.Configuration["ApiGateway:BaseUrl"]
    ?? throw new InvalidOperationException("Missing ApiGateway:BaseUrl");
var urlsTableName = builder.Configuration["AWS:Resources:UrlsTableName"]
    ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
var analyticsTableName = builder.Configuration["AWS:Resources:AnalyticsTableName"]
    ?? throw new InvalidOperationException("Missing AWS:Resources:AnalyticsTableName");

builder.Services.AddHttpClient(apiGatewayHttpClientName, client => client.BaseAddress = new Uri(apiGatewayBaseUrl));

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", (HttpRequest request) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    return Results.Ok(new ConfigResponse(apiGatewayBaseUrl));
});

app.MapGet("/api/snapshot", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var links = await LoadLinksAsync(dynamoDb, cancellationToken).ConfigureAwait(false);
    var analyticsEvents = await LoadAnalyticsEventsAsync(dynamoDb, cancellationToken).ConfigureAwait(false);
    var timelineEvents = BuildTimelineEvents(links, analyticsEvents);

    return Results.Ok(new CommandCenterSnapshot(links, analyticsEvents, timelineEvents));
});

app.MapGet("/api/links", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var links = await LoadLinksAsync(dynamoDb, cancellationToken).ConfigureAwait(false);

    return Results.Ok(links);
});

app.MapGet("/api/analytics", async (HttpRequest request, IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    using var telemetryScope = CommandCenterRefreshTelemetry.SuppressIfRefreshTracingDisabled(request);

    var events = await LoadAnalyticsEventsAsync(dynamoDb, cancellationToken).ConfigureAwait(false);

    return Results.Ok(events);
});

app.MapPost("/api/shorten", async (HttpRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

    var client = httpClientFactory.CreateClient(apiGatewayHttpClientName);

    try
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var upstreamResponse = await client.PostAsync(new Uri("/shorten", UriKind.Relative), content, cancellationToken).ConfigureAwait(false);
        var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return Results.Content(responseBody, "application/json", statusCode: (int)upstreamResponse.StatusCode);
    }
    catch (HttpRequestException ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "API Gateway unreachable");
    }
});

await app.RunAsync().ConfigureAwait(false);

async Task<List<LinkSummary>> LoadLinksAsync(IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken)
{
    var scanResponse = await dynamoDb.ScanAsync(new ScanRequest { TableName = urlsTableName }, cancellationToken).ConfigureAwait(false);

    return [.. scanResponse.Items
        .Select(DynamoDbItemMapper.ToLinkSummary)
        .OrderByDescending(link => link.CreatedAt, StringComparer.Ordinal)
        .Take(maxFeedItems)];
}

async Task<List<AnalyticsEventSummary>> LoadAnalyticsEventsAsync(IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken)
{
    var scanResponse = await dynamoDb.ScanAsync(new ScanRequest { TableName = analyticsTableName }, cancellationToken).ConfigureAwait(false);

    return [.. scanResponse.Items
        .Select(DynamoDbItemMapper.ToAnalyticsEventSummary)
        .OrderByDescending(analyticsEvent => analyticsEvent.Timestamp, StringComparer.Ordinal)
        .Take(maxFeedItems)];
}

static List<TimelineEventSummary> BuildTimelineEvents(
    IEnumerable<LinkSummary> links,
    IEnumerable<AnalyticsEventSummary> analyticsEvents)
{
    List<TimelineEventSummary> timelineEvents = [];

    foreach (var link in links)
    {
        timelineEvents.Add(new TimelineEventSummary(
            link.CreatedAt,
            "LinkCreated",
            link.Slug,
            $"/{link.Slug} created with QR status {link.QrStatus}"));

        if (!string.IsNullOrWhiteSpace(link.QrGeneratedAt))
        {
            timelineEvents.Add(new TimelineEventSummary(
                link.QrGeneratedAt,
                "QrReady",
                link.Slug,
                $"QR PNG stored at {link.QrObjectKey}"));
        }
    }

    foreach (var analyticsEvent in analyticsEvents)
    {
        var eventType = string.Equals(analyticsEvent.EventType, "url_accessed", StringComparison.Ordinal)
            ? "LinkAccessed"
            : "AnalyticsRecorded";

        timelineEvents.Add(new TimelineEventSummary(
            analyticsEvent.Timestamp,
            eventType,
            analyticsEvent.Slug,
            $"{analyticsEvent.EventType} for /{analyticsEvent.Slug}"));
    }

    return [.. timelineEvents
        .OrderByDescending(timelineEvent => timelineEvent.Timestamp, StringComparer.Ordinal)
        .Take(maxFeedItems)];
}
