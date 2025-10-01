#pragma warning disable CA1822 // Member 'FunctionHandler' does not access instance data and can be marked as static
#pragma warning disable S2325 // Make 'FunctionHandler' a static method.
#pragma warning disable CA1812 // Error CA1812 : 'Function.ShortenRequest' is an internal class that is apparently never instantiated.
#pragma warning disable CA1031 // Modify 'FunctionHandler' to catch a more specific allowed exception type, or rethrow the exception

using System.Diagnostics;
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

namespace LocalStack.Lambda.Redirector;

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
            using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));

            var slug = request.PathParameters?["slug"] ?? request.PathParameters?.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(slug))
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                return NotFound();
            }

            var dbResp = await _amazonDynamoDb.GetItemAsync(_urlsTable,
                new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
                {
                    ["Slug"] = new() { S = slug },
                }).ConfigureAwait(false);

            if (!dbResp.IsItemSet)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddTag("slug", slug);
                return NotFound();
            }

            var originalUrl = dbResp.Item["Url"].S;

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

public sealed record AnalyticsEvent(
    string EventType, // "url_created" or "url_accessed"
    string Slug,
    string OriginalUrl,
    string? UserAgent = null,
    string? IpAddress = null);
