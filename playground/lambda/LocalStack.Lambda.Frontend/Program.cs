using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LocalStack.Client.Extensions;
using LocalStack.Lambda.Frontend;

const string apiGatewayHttpClientName = "ApiGateway";
const int maxFeedItems = 25;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

app.MapGet("/api/config", () => Results.Ok(new ConfigResponse(apiGatewayBaseUrl)));

app.MapGet("/api/links", async (IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    var scanResponse = await dynamoDb.ScanAsync(new ScanRequest { TableName = urlsTableName }, cancellationToken).ConfigureAwait(false);

    var links = scanResponse.Items
        .Select(DynamoDbItemMapper.ToLinkSummary)
        .OrderByDescending(link => link.CreatedAt, StringComparer.Ordinal)
        .Take(maxFeedItems)
        .ToList();

    return Results.Ok(links);
});

app.MapGet("/api/analytics", async (IAmazonDynamoDB dynamoDb, CancellationToken cancellationToken) =>
{
    var scanResponse = await dynamoDb.ScanAsync(new ScanRequest { TableName = analyticsTableName }, cancellationToken).ConfigureAwait(false);

    var events = scanResponse.Items
        .Select(DynamoDbItemMapper.ToAnalyticsEventSummary)
        .OrderByDescending(analyticsEvent => analyticsEvent.Timestamp, StringComparer.Ordinal)
        .Take(maxFeedItems)
        .ToList();

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
