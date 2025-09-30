#pragma warning disable CA1822 // Member 'FunctionHandler' does not access instance data and can be marked as static
#pragma warning disable S2325 // Make 'FunctionHandler' a static method.
#pragma warning disable CA1812 // Error CA1812 : 'Function.ShortenRequest' is an internal class that is apparently never instantiated.
#pragma warning disable CA1031 // Modify 'FunctionHandler' to catch a more specific allowed exception type, or rethrow the exception

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using LocalStack.Client.Extensions;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Codecrete.QrCodeGenerator;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.UrlShortener;

public class Function
{
    private readonly TracerProvider _traceProvider;

    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonS3 _amazonS3;
    private readonly IAmazonSQS _amazonSqs;

    private readonly IS3UrlService _s3UrlService;

    private readonly string _qrBucketName;
    private readonly string _urlsTable;
    private readonly string _analyticsQueueUrl;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();
        builder.Services.AddAwsService<IAmazonS3>();
        builder.Services.AddAwsService<IAmazonSQS>();

        builder.Services.AddTransient<IS3UrlService, S3UrlService>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();
        _amazonS3 = host.Services.GetRequiredService<IAmazonS3>();
        _amazonSqs = host.Services.GetRequiredService<IAmazonSQS>();
        _s3UrlService = host.Services.GetRequiredService<IS3UrlService>();

        _qrBucketName = builder.Configuration["AWS:Resources:QrBucketName"] ?? throw new InvalidOperationException("Missing AWS:Resources:QrBucketName");
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

            string? qrUrl = null;

            // Optional QR‑code branch
            if (string.Equals(payload.Format, "qr", StringComparison.OrdinalIgnoreCase))
            {
                qrUrl = await GenerateAndUploadQrAsync(slug, payload.Url).ConfigureAwait(false);
                await UpdateRecordWithQrUrlAsync(slug, qrUrl).ConfigureAwait(false);
            }

            var responseBody = JsonSerializer.Serialize(new ShortenResponse(slug, qrUrl));

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
            },
            ConditionExpression = "attribute_not_exists(Slug)",
        }).ConfigureAwait(false);
    }

    private async Task UpdateRecordWithQrUrlAsync(string slug, string qrUrl)
    {
        using var activity = UrlShortenerActivitySource.ActivitySource.StartActivity(nameof(UpdateRecordWithQrUrlAsync));
        activity?.AddTag("slug", slug);
        activity?.AddTag("qrUrl", qrUrl);

        await _amazonDynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _urlsTable,
            Key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["Slug"] = new() { S = slug },
            },
            UpdateExpression = "SET QrUrl = :qrUrl",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                [":qrUrl"] = new() { S = qrUrl },
            },
        }).ConfigureAwait(false);
    }

    private async Task<string> GenerateAndUploadQrAsync(string slug, string originalUrl)
    {
        using var activity = UrlShortenerActivitySource.ActivitySource.StartActivity(nameof(GenerateAndUploadQrAsync));
        activity?.AddTag("originalUrl", originalUrl);
        activity?.AddTag("slug", slug);
        activity?.AddTag("qrBucketName", _qrBucketName);

        var qrCode = QrCode.EncodeText(originalUrl, QrCode.Ecc.Quartile);
        var pngData = qrCode.ToPng(scale: 10, border: 4);

        var key = $"qr/{slug}.png";
        await _amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _qrBucketName,
            Key = key,
            InputStream = new MemoryStream(pngData),
            ContentType = "image/png",
        }).ConfigureAwait(false);

        var s3Url = _s3UrlService.GetS3Url(_amazonS3, _qrBucketName, key);

        return s3Url;
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

        context.Logger.LogInformation($"Sent analytics event for slug: {slug}");
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

    private sealed record ShortenRequest(string Url, string? Format);

    private sealed record ShortenResponse(string Id, string? QrUrl);
}

public sealed record AnalyticsEvent(
    string EventType,  // "url_created" or "url_accessed"
    string Slug,
    string OriginalUrl,
    string? UserAgent = null,
    string? IpAddress = null);
