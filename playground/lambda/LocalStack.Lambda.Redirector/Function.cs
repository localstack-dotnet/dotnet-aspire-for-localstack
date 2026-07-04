using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
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

namespace LocalStack.Lambda.Redirector;

public class Function
{
    private const string QrRouteKey = "GET /{slug}/qr";
    private const string QrPathSuffix = "/qr";

    private readonly TracerProvider _traceProvider;

    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonSQS _amazonSqs;
    private readonly IAmazonS3 _amazonS3;
    private readonly IS3UrlService _s3UrlService;

    private readonly string _urlsTable;
    private readonly string _analyticsQueueUrl;
    private readonly string _qrBucketName;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();
        builder.Services.AddAwsService<IAmazonSQS>();
        builder.Services.AddAwsService<IAmazonS3>();

        builder.Services.AddTransient<IS3UrlService, S3UrlService>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();
        _amazonSqs = host.Services.GetRequiredService<IAmazonSQS>();
        _amazonS3 = host.Services.GetRequiredService<IAmazonS3>();
        _s3UrlService = host.Services.GetRequiredService<IS3UrlService>();

        _urlsTable = builder.Configuration["AWS:Resources:UrlsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
        _analyticsQueueUrl = builder.Configuration["AWS:Resources:AnalyticsQueueUrl"] ?? throw new InvalidOperationException("Missing AWS:Resources:AnalyticsQueueUrl");
        _qrBucketName = builder.Configuration["AWS:Resources:QrBucketName"] ?? throw new InvalidOperationException("Missing AWS:Resources:QrBucketName");
    }

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (proxyRequest, lambdaContext) =>
        {
            using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));

            var slug = request.PathParameters?["slug"] ?? request.PathParameters?.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(slug))
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                return NotFound();
            }

            if (IsQrStatusRoute(proxyRequest))
            {
                return await HandleQrStatusAsync(slug, lambdaContext).ConfigureAwait(false);
            }

            var item = await GetUrlItemAsync(slug).ConfigureAwait(false);
            if (item is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddTag("slug", slug);
                return NotFound();
            }

            var originalUrl = item["Url"].S;

            // Send analytics event (fire-and-forget, don't block the redirect)
            try
            {
                await SendAnalyticsEventAsync(slug, originalUrl, proxyRequest, lambdaContext).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log but don't throw - analytics shouldn't break redirects
                lambdaContext.Logger.LogWarning($"Failed to send analytics event: {ex.Message}");
            }

            return Found(originalUrl);
        }, request, context);
    }

    private static bool IsQrStatusRoute(APIGatewayHttpApiV2ProxyRequest proxyRequest) =>
        string.Equals(proxyRequest.RequestContext?.RouteKey, QrRouteKey, StringComparison.Ordinal)
        || (proxyRequest.RawPath?.EndsWith(QrPathSuffix, StringComparison.Ordinal) ?? false);

    private async Task<Dictionary<string, AttributeValue>?> GetUrlItemAsync(string slug)
    {
        var dbResp = await _amazonDynamoDb.GetItemAsync(_urlsTable,
            new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["Slug"] = new() { S = slug },
            }).ConfigureAwait(false);

        return dbResp.IsItemSet ? dbResp.Item : null;
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> HandleQrStatusAsync(string slug, ILambdaContext context)
    {
        using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(HandleQrStatusAsync));
        activity?.AddTag("slug", slug);

        var item = await GetUrlItemAsync(slug).ConfigureAwait(false);
        if (item is null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return NotFound();
        }

        var sanitizedSlug = slug.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);

        var isReady = item.TryGetValue("QrStatus", out var status)
                      && string.Equals(status.S, "Ready", StringComparison.Ordinal)
                      && item.TryGetValue("QrObjectKey", out _);

        if (!isReady)
        {
            context.Logger.LogInformation($"QR code pending for slug: {sanitizedSlug}");

            return new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = (int)HttpStatusCode.Accepted,
                Body = JsonSerializer.Serialize(new { slug, qrStatus = "Pending" }),
                Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = "application/json" },
            };
        }

        var qrUrl = _s3UrlService.GetS3Url(_amazonS3, _qrBucketName, item["QrObjectKey"].S);

        context.Logger.LogInformation($"Redirecting to QR code for slug: {sanitizedSlug}");

        return new APIGatewayHttpApiV2ProxyResponse
        {
            StatusCode = (int)HttpStatusCode.Found,
            Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Location"] = qrUrl },
        };
    }

    private async Task SendAnalyticsEventAsync(string slug, string originalUrl, APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(SendAnalyticsEventAsync));

        var userAgent = request.RequestContext?.Http?.UserAgent ?? "unknown";
        var ipAddress = request.RequestContext?.Http?.SourceIp ?? "unknown";

        var analyticsEvent = new AnalyticsEvent("url_accessed", slug, originalUrl, userAgent, ipAddress);

        await _amazonSqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _analyticsQueueUrl,
            MessageBody = JsonSerializer.Serialize(analyticsEvent),
        }).ConfigureAwait(false);

        var sanitizedSlug = slug.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        context.Logger.LogInformation($"Sent analytics event for slug: {sanitizedSlug}");
    }

    private static APIGatewayHttpApiV2ProxyResponse Found(string originalUrl) => new()
    {
        StatusCode = (int)HttpStatusCode.Found,
        Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Location"] = originalUrl },
    };

    private static APIGatewayHttpApiV2ProxyResponse NotFound() => new()
    {
        StatusCode = (int)HttpStatusCode.NotFound,
        Body = string.Empty,
    };
}

internal sealed record AnalyticsEvent(
    string EventType, // "url_created" or "url_accessed"
    string Slug,
    string OriginalUrl,
    string? UserAgent = null,
    string? IpAddress = null);
