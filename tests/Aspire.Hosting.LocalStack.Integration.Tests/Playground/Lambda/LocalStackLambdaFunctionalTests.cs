using System.Net.Http.Json;
using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// End-to-end functional tests for the Lambda playground.
/// These tests validate the complete URL shortener and analytics flow.
/// </summary>
[Collection("LocalStackLambda")]
public class LocalStackLambdaFunctionalTests(LocalStackLambdaFixture fixture, ITestOutputHelper outputHelper)
{
    // Cache JsonSerializerOptions to avoid creating new instances for each test (CA1869)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // JsonSerializerOptions for posting requests - use default naming (PascalCase) to match Lambda expectations
    private static readonly JsonSerializerOptions PostJsonOptions = new()
    {
        PropertyNamingPolicy = null, // Use default PascalCase, not camelCase
    };

    [Fact]
    public async Task UrlShortener_Should_Create_Short_Url()
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        var request = new { Url = "https://aws.amazon.com", Format = (string?)null };

        // Act
        var response = await httpClient.PostAsJsonAsync("/shorten", request, PostJsonOptions, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Log response for debugging
        outputHelper.WriteLine($"Response Status: {response.StatusCode}");
        outputHelper.WriteLine($"Response Body: {content}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = JsonSerializer.Deserialize<ShortenResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Null(result.QrUrl); // No QR code requested

        outputHelper.WriteLine($"Created short URL with ID: {result.Id}");
    }

    [Fact]
    public async Task UrlShortener_Should_Create_Short_Url_With_QrCode()
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        var request = new { Url = "https://localstack.cloud", Format = "qr" };

        // Act
        var response = await httpClient.PostAsJsonAsync("/shorten", request, PostJsonOptions, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = JsonSerializer.Deserialize<ShortenResponse>(content, JsonOptions);
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.NotNull(result.QrUrl); // QR code was requested
        Assert.NotEmpty(result.QrUrl);

        outputHelper.WriteLine($"Created short URL with ID: {result.Id}");
        outputHelper.WriteLine($"QR Code URL: {result.QrUrl}");
    }

    [Fact]
    public async Task Redirector_Should_Redirect_To_Original_Url()
    {
        // Arrange: First create a short URL
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        var createRequest = new { Url = "https://docs.localstack.cloud", Format = (string?)null };
        var createResponse = await httpClient.PostAsJsonAsync("/shorten", createRequest, PostJsonOptions, TestContext.Current.CancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        Assert.NotNull(createResult);
        Assert.NotNull(createResult.Id);

        // Act: Access the redirect endpoint (don't follow redirects automatically)
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        using var redirectClient = new HttpClient(handler);
        redirectClient.BaseAddress = httpClient.BaseAddress;
        redirectClient.Timeout = TimeSpan.FromSeconds(60);

        var redirectResponse = await redirectClient.GetAsync(new Uri($"/{createResult.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode); // 302 Found
        Assert.True(redirectResponse.Headers.Location is not null);
        // Trim trailing slash for comparison as DynamoDB might normalize URLs
        Assert.Equal("https://docs.localstack.cloud", redirectResponse.Headers.Location.ToString().TrimEnd('/'));

        outputHelper.WriteLine($"Redirect from /{createResult.Id} to {redirectResponse.Headers.Location}");
    }

    [Fact]
    public async Task Analyzer_Lambda_Should_Process_Events_From_Queue()
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        using var dynamoDbClient = LocalStackTestHelpers.CreateDynamoDbClient(fixture.LocalStackConnectionString, fixture.RegionName);
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                                 ?? throw new InvalidOperationException("AnalyticsTableName not found");

        var testUrl = $"https://analyzer-test.example.com/{Guid.NewGuid()}";
        var payload = $$"""{ "Url": "{{testUrl}}", "Format": "qr" }""";
        using var stringContent = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        // Act: Create a short URL (this triggers analytics event → SQS → Analyzer Lambda)
        var createResponse = await httpClient.PostAsync(new Uri("/shorten", UriKind.Relative), stringContent, TestContext.Current.CancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        Assert.NotNull(createResult);
        Assert.NotNull(createResult.Id);

        // Wait for SQS Event Source to trigger Analyzer Lambda and process the event
        outputHelper.WriteLine("Waiting for Analyzer Lambda to process event...");
        await Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: Verify event was written to AnalyticsTable by Analyzer Lambda
        var scanResponse = await dynamoDbClient.ScanAsync(new ScanRequest
        {
            TableName = analyticsTableName,
            FilterExpression = "Slug = :slug AND EventType = :eventType",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                (StringComparer.OrdinalIgnoreCase)
                {
                    [":slug"] = new() { S = createResult.Id },
                    [":eventType"] = new() { S = "url_created" },
                },
        }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(scanResponse.Items);
        Assert.Single(scanResponse.Items);

        var analyticsItem = scanResponse.Items[0];
        Assert.Equal(createResult.Id, analyticsItem["Slug"].S);
        Assert.Equal("url_created", analyticsItem["EventType"].S);
        Assert.Equal(testUrl, analyticsItem["OriginalUrl"].S);

        outputHelper.WriteLine($"Analyzer Lambda successfully processed event for slug: {createResult.Id}");
    }

    [Fact]
    public async Task Redirecting_Url_Should_Send_Analytics_Event_And_Be_Processed()
    {
        // Arrange: First create a short URL
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(60);

        using var dynamoDbClient = LocalStackTestHelpers.CreateDynamoDbClient(fixture.LocalStackConnectionString, fixture.RegionName);
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                                 ?? throw new InvalidOperationException("AnalyticsTableName not found");

        var testUrl = $"https://redirect-analytics-test.example.com/{Guid.NewGuid()}";
        var createRequest = new { Url = testUrl, Format = (string?)null };
        var createResponse = await httpClient.PostAsJsonAsync("/shorten", createRequest, PostJsonOptions, TestContext.Current.CancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        Assert.NotNull(createResult);
        Assert.NotNull(createResult.Id);

        // Wait for creation analytics to be processed
        await Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Act: Access the redirect endpoint
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        using var redirectClient = new HttpClient(handler);
        redirectClient.BaseAddress = httpClient.BaseAddress;
        redirectClient.Timeout = TimeSpan.FromSeconds(60);

        var redirectResponse = await redirectClient.GetAsync(new Uri($"/{createResult.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, redirectResponse.StatusCode);

        // Wait for Analyzer Lambda to process the url_accessed event
        outputHelper.WriteLine("Waiting for Analyzer Lambda to process url_accessed event...");
        await Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert: Verify both url_created and url_accessed events are in AnalyticsTable
        var scanResponse = await dynamoDbClient.ScanAsync(new ScanRequest
        {
            TableName = analyticsTableName,
            FilterExpression = "Slug = :slug",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                (StringComparer.OrdinalIgnoreCase)
                {
                    [":slug"] = new() { S = createResult.Id },
                },
        }, TestContext.Current.CancellationToken);

        Assert.NotEmpty(scanResponse.Items);
        Assert.Equal(2, scanResponse.Items.Count); // Both url_created and url_accessed

        var eventTypes = scanResponse.Items.Select(item => item["EventType"].S).ToList();
        Assert.Contains("url_created", eventTypes);
        Assert.Contains("url_accessed", eventTypes);

        outputHelper.WriteLine($"Successfully verified both url_created and url_accessed events for slug: {createResult.Id}");
    }

    /// <summary>
    /// Response from the URL shortener Lambda function.
    /// </summary>
#pragma warning disable CA1812 // Class is instantiated via JSON deserialization
    private sealed record ShortenResponse(string? Id, string? QrUrl);
#pragma warning restore CA1812
}
