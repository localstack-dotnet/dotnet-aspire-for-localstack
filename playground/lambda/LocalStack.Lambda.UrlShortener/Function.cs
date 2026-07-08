using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using LocalStack.Client.Extensions;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.UrlShortener;

public class Function
{
    private readonly TracerProvider _traceProvider;

    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonSQS _amazonSqs;

    private readonly string _urlsTable;
    private readonly string _analyticsQueueUrl;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();
        builder.Services.AddAwsService<IAmazonSQS>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();
        _amazonSqs = host.Services.GetRequiredService<IAmazonSQS>();

        _urlsTable = builder.Configuration["AWS:Resources:UrlsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
        _analyticsQueueUrl = builder.Configuration["AWS:Resources:AnalyticsQueueUrl"] ?? throw new InvalidOperationException("Missing AWS:Resources:AnalyticsQueueUrl");
    }

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (proxyRequest, lambdaContext) =>
        {
            using var activity = UrlShortenerActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));

            if (string.IsNullOrWhiteSpace(proxyRequest.Body))
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                return BadRequest("Empty body");
            }

            var payload = JsonSerializer.Deserialize<ShortenRequest>(proxyRequest.Body);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Url))
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddTag("payload", proxyRequest.Body);
                return BadRequest("Missing 'url' property");
            }

            var slug = SlugGenerator.Create();

            await InsertRecordAsync(slug, payload.Url).ConfigureAwait(false);

            // Send analytics event (fire-and-forget, don't block URL creation)
            try
            {
                await SendAnalyticsEventAsync(slug, payload.Url, proxyRequest, lambdaContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but don't throw - analytics shouldn't break URL creation
                lambdaContext.Logger.LogWarning($"Failed to send analytics event: {ex.Message}");
            }

            var responseBody = JsonSerializer.Serialize(new ShortenResponse(slug, "Pending", $"/{slug}/qr"));

            return Created(responseBody);
        }, request, context);
    }

    private async Task InsertRecordAsync(string slug, string url)
    {
        using var activity = UrlShortenerActivitySource.ActivitySource.StartActivity(nameof(InsertRecordAsync));
        activity?.AddTag("slug", slug);
        activity?.AddTag("url", url);

        await _amazonDynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _urlsTable,
            Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["Slug"] = new() { S = slug },
                ["Url"] = new() { S = url },
                ["CreatedAt"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
                ["QrStatus"] = new() { S = "Pending" },
            },
            ConditionExpression = "attribute_not_exists(Slug)",
        }).ConfigureAwait(false);
    }

    private async Task SendAnalyticsEventAsync(string slug, string originalUrl, APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        using var activity = UrlShortenerActivitySource.ActivitySource.StartActivity(nameof(SendAnalyticsEventAsync));

        var userAgent = request.RequestContext?.Http?.UserAgent ?? "unknown";
        var ipAddress = request.RequestContext?.Http?.SourceIp ?? "unknown";

        var analyticsEvent = new AnalyticsEvent("url_created", slug, originalUrl, userAgent, ipAddress);

        await _amazonSqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _analyticsQueueUrl,
            MessageBody = JsonSerializer.Serialize(analyticsEvent),
        }).ConfigureAwait(false);

        var sanitizedSlug = slug.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        context.Logger.LogInformation($"Sent analytics event for slug: {sanitizedSlug}");
    }

    private static APIGatewayHttpApiV2ProxyResponse BadRequest(string msg) => new()
    {
        StatusCode = (int)HttpStatusCode.BadRequest,
        Body = JsonSerializer.Serialize(new { error = msg }),
        Headers = new Dictionary<string, string>
            (StringComparer.Ordinal)
            {
                { "Content-Type", "application/json" },
            },
    };

    private static APIGatewayHttpApiV2ProxyResponse Created(string responseBody) => new()
    {
        StatusCode = (int)HttpStatusCode.Created,
        Body = responseBody,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = "application/json" },
    };

    private sealed record ShortenRequest(string Url);

    private sealed record ShortenResponse(string Id, string QrStatus, string QrPath);
}

internal sealed record AnalyticsEvent(
    string EventType, // "url_created" or "url_accessed"
    string Slug,
    string OriginalUrl,
    string? UserAgent = null,
    string? IpAddress = null);
