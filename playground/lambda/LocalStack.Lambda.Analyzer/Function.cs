#pragma warning disable CA1822 // Member 'FunctionHandler' does not access instance data and can be marked as static
#pragma warning disable S2325 // Make 'FunctionHandler' a static method.
#pragma warning disable CA1812 // Error CA1812 : 'Function.ShortenRequest' is an internal class that is apparently never instantiated.
#pragma warning disable CA1031 // Modify 'FunctionHandler' to catch a more specific allowed exception type, or rethrow the exception

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using LocalStack.Client.Extensions;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.Analyzer;

public class Function
{
    private readonly TracerProvider _traceProvider;

    private readonly IAmazonDynamoDB _amazonDynamoDb;

    private readonly string _analyticsTable;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();

        _analyticsTable = builder.Configuration["AWS:Resources:AnalyticsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:AnalyticsTableName");
    }

    public Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (proxyRequest, lambdaContext) =>
        {
            using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));

            lambdaContext.Logger.LogInformation($"Processing {proxyRequest.Records.Count} analytics events");

            foreach (var record in proxyRequest.Records)
            {
                try
                {
                    var analyticsEvent = JsonSerializer.Deserialize<AnalyticsEvent>(record.Body);
                    if (analyticsEvent != null)
                    {
                        await ProcessAnalyticsEventAsync(analyticsEvent, lambdaContext).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    lambdaContext.Logger.LogError($"Error processing analytics event: {ex.Message}");
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                }
            }
        }, sqsEvent, context);
    }

    private async Task ProcessAnalyticsEventAsync(AnalyticsEvent analyticsEvent, ILambdaContext context)
    {
        using var activity = RedirectorActivitySource.ActivitySource.StartActivity(nameof(ProcessAnalyticsEventAsync));

        activity?.AddTag("eventType", analyticsEvent.EventType);
        activity?.AddTag("slug", analyticsEvent.Slug);

        var item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            ["EventId"] = new() { S = Guid.NewGuid().ToString() },
            ["Timestamp"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            ["EventType"] = new() { S = analyticsEvent.EventType },
            ["Slug"] = new() { S = analyticsEvent.Slug },
            ["OriginalUrl"] = new() { S = analyticsEvent.OriginalUrl },
            ["UserAgent"] = new() { S = analyticsEvent.UserAgent ?? "unknown" },
            ["IpAddress"] = new() { S = analyticsEvent.IpAddress ?? "unknown" },
        };

        await _amazonDynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _analyticsTable,
            Item = item,
        }).ConfigureAwait(false);

        context.Logger.LogInformation($"Processed {analyticsEvent.EventType} event for slug: {analyticsEvent.Slug}");
    }
}

public sealed record AnalyticsEvent(
    string EventType,  // "url_created" or "url_accessed"
    string Slug,
    string OriginalUrl,
    string? UserAgent = null,
    string? IpAddress = null);
