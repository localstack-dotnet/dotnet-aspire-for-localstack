#pragma warning disable CA1822 // Member 'FunctionHandler' does not access instance data and can be marked as static
#pragma warning disable S2325 // Make 'FunctionHandler' a static method.
#pragma warning disable CA1812 // Error CA1812 : 'Function.ShortenRequest' is an internal class that is apparently never instantiated.

using System.Diagnostics;
using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
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

    private readonly string _urlsTable;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();

        _urlsTable = builder.Configuration["AWS:Resources:UrlsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
    }

    public Task<APIGatewayHttpApiV2ProxyResponse> FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (proxyRequest, _) =>
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
            return Found(originalUrl);
        }, request, context);
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
