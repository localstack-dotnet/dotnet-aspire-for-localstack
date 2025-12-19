namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// End-to-end functional tests for the Lambda playground.
/// These tests validate the complete URL shortener and analytics flow.
/// </summary>
[NotInParallel("IntegrationTests")]
[ClassDataSource<LocalStackLambdaFixture>(Shared = SharedType.PerTestSession)]
public class LocalStackLambdaFunctionalTests(LocalStackLambdaFixture fixture)
{
    // Cache JsonSerializerOptions to avoid creating new instances for each test (CA1869)
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // JsonSerializerOptions for posting requests - use default naming (PascalCase) to match Lambda expectations
    private static readonly JsonSerializerOptions PostJsonOptions = new()
    {
        PropertyNamingPolicy = null, // Use default PascalCase, not camelCase
    };

    [Test]
    public async Task UrlShortener_Should_Create_Short_Url(CancellationToken cancellationToken)
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        var request = new { Url = "https://aws.amazon.com", Format = (string?)null };

        // Act
        var response = await httpClient.PostAsJsonAsync("/shorten", request, PostJsonOptions, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        // Log response for debugging
        await TestOutputHelper.WriteLineAsync($"Response Status: {response.StatusCode}");
        await TestOutputHelper.WriteLineAsync($"Response Body: {content}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var result = JsonSerializer.Deserialize<ShortenResponse>(content, JsonOptions);
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsNotNull();
        await Assert.That(result.Id).IsNotEmpty();
        await Assert.That(result.QrUrl).IsNull(); // No QR code requested

        await TestOutputHelper.WriteLineAsync($"Created short URL with ID: {result.Id}");
    }

    [Test]
    public async Task UrlShortener_Should_Create_Short_Url_With_QrCode(CancellationToken cancellationToken)
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        var request = new { Url = "https://localstack.cloud", Format = "qr" };

        // Act
        var response = await httpClient.PostAsJsonAsync("/shorten", request, PostJsonOptions, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var result = JsonSerializer.Deserialize<ShortenResponse>(content, JsonOptions);
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsNotNull();
        await Assert.That(result.Id).IsNotEmpty();
        await Assert.That(result.QrUrl).IsNotNull(); // QR code was requested
        await Assert.That(result.QrUrl).IsNotEmpty();

        await TestOutputHelper.WriteLineAsync($"Created short URL with ID: {result.Id}");
        await TestOutputHelper.WriteLineAsync($"QR Code URL: {result.QrUrl}");
    }

    [Test]
    public async Task Redirector_Should_Redirect_To_Original_Url(CancellationToken cancellationToken)
    {
        // Arrange: First create a short URL
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(90);

        var createRequest = new { Url = "https://docs.localstack.cloud", Format = (string?)null };
        var createResponse = await httpClient.PostAsJsonAsync("/shorten", createRequest, PostJsonOptions, cancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        await Assert.That(createResult).IsNotNull();
        await Assert.That(createResult.Id).IsNotNull();

        // Act: Access the redirect endpoint (don't follow redirects automatically)
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        using var redirectClient = new HttpClient(handler);
        redirectClient.BaseAddress = httpClient.BaseAddress;
        redirectClient.Timeout = TimeSpan.FromSeconds(90);

        var redirectResponse = await redirectClient.GetAsync(new Uri($"/{createResult.Id}", UriKind.Relative), cancellationToken);
        var headersLocation = redirectResponse.Headers.Location;

        // Assert
        await Assert.That(redirectResponse.StatusCode).IsEqualTo(HttpStatusCode.Found); // 302 Found
        await Assert.That(headersLocation).IsNotNull();
        // Trim trailing slash for comparison as DynamoDB might normalize URLs
        await Assert.That(headersLocation.ToString().TrimEnd('/')).IsEqualTo("https://docs.localstack.cloud");

        await TestOutputHelper.WriteLineAsync($"Redirect from /{createResult.Id} to {headersLocation}");
    }

    [Test]
    public async Task Analyzer_Lambda_Should_Process_Events_From_Queue(CancellationToken cancellationToken)
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(90);

        using var dynamoDbClient = LocalStackTestHelpers.CreateDynamoDbClient(fixture.LocalStackConnectionString, fixture.RegionName);
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                                 ?? throw new InvalidOperationException("AnalyticsTableName not found");

        var testUrl = $"https://analyzer-test.example.com/{Guid.NewGuid()}";
        var payload = $$"""{ "Url": "{{testUrl}}", "Format": "qr" }""";
        using var stringContent = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        // Act: Create a short URL (this triggers analytics event → SQS → Analyzer Lambda)
        var createResponse = await httpClient.PostAsync(new Uri("/shorten", UriKind.Relative), stringContent, cancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        await Assert.That(createResult).IsNotNull();
        await Assert.That(createResult.Id).IsNotNull();

        // Wait for SQS Event Source to trigger Analyzer Lambda and process the event
        await TestOutputHelper.WriteLineAsync("Waiting for Analyzer Lambda to process event...");
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Assert: Verify event was written to AnalyticsTable by Analyzer Lambda
        var scanResponse = await dynamoDbClient.ScanAsync(new Amazon.DynamoDBv2.Model.ScanRequest
        {
            TableName = analyticsTableName,
            FilterExpression = "Slug = :slug AND EventType = :eventType",
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
                (StringComparer.OrdinalIgnoreCase)
                {
                    [":slug"] = new() { S = createResult.Id },
                    [":eventType"] = new() { S = "url_created" },
                },
        }, cancellationToken);

        await Assert.That(scanResponse.Items).IsNotEmpty();
        await Assert.That(scanResponse.Items).HasSingleItem();

        var analyticsItem = scanResponse.Items[0];
        await Assert.That(analyticsItem["Slug"].S).IsEqualTo(createResult.Id);
        await Assert.That(analyticsItem["EventType"].S).IsEqualTo("url_created");
        await Assert.That(analyticsItem["OriginalUrl"].S).IsEqualTo(testUrl);

        await TestOutputHelper.WriteLineAsync($"Analyzer Lambda successfully processed event for slug: {createResult.Id}");
    }

    [Test]
    public async Task Redirecting_Url_Should_Send_Analytics_Event_And_Be_Processed(CancellationToken cancellationToken)
    {
        // Arrange: First create a short URL
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(90);

        using var dynamoDbClient = LocalStackTestHelpers.CreateDynamoDbClient(fixture.LocalStackConnectionString, fixture.RegionName);
        var analyticsTableName = fixture.StackOutputs.GetOutput("AnalyticsTableName")
                                 ?? throw new InvalidOperationException("AnalyticsTableName not found");

        var testUrl = $"https://redirect-analytics-test.example.com/{Guid.NewGuid()}";
        var createRequest = new { Url = testUrl, Format = (string?)null };
        var createResponse = await httpClient.PostAsJsonAsync("/shorten", createRequest, PostJsonOptions, cancellationToken);
        var createContent = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        var createResult = JsonSerializer.Deserialize<ShortenResponse>(createContent, JsonOptions);

        await Assert.That(createResult).IsNotNull();
        await Assert.That(createResult.Id).IsNotNull();

        // Wait for creation analytics to be processed
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Act: Access the redirect endpoint
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        using var redirectClient = new HttpClient(handler);
        redirectClient.BaseAddress = httpClient.BaseAddress;
        redirectClient.Timeout = TimeSpan.FromSeconds(90);

        var redirectResponse = await redirectClient.GetAsync(new Uri($"/{createResult.Id}", UriKind.Relative), cancellationToken);
        await Assert.That(redirectResponse.StatusCode).IsEqualTo(HttpStatusCode.Found);

        // Wait for Analyzer Lambda to process the url_accessed event
        await TestOutputHelper.WriteLineAsync("Waiting for Analyzer Lambda to process url_accessed event...");
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Assert: Verify both url_created and url_accessed events are in AnalyticsTable
        var scanResponse = await dynamoDbClient.ScanAsync(new Amazon.DynamoDBv2.Model.ScanRequest
        {
            TableName = analyticsTableName,
            FilterExpression = "Slug = :slug",
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
                (StringComparer.OrdinalIgnoreCase)
                {
                    [":slug"] = new() { S = createResult.Id },
                },
        }, cancellationToken);

        await Assert.That(scanResponse.Items).IsNotEmpty();
        await Assert.That(scanResponse.Items.Count).IsEqualTo(2); // Both url_created and url_accessed

        var eventTypes = scanResponse.Items.Select(item => item["EventType"].S).ToList();
        await Assert.That(eventTypes).Contains("url_created");
        await Assert.That(eventTypes).Contains("url_accessed");

        await TestOutputHelper.WriteLineAsync($"Successfully verified both url_created and url_accessed events for slug: {createResult.Id}");
    }

    /// <summary>
    /// Response from the URL shortener Lambda function.
    /// </summary>
#pragma warning disable CA1812 // Class is instantiated via JSON deserialization
    private sealed record ShortenResponse(string? Id, string? QrUrl);
#pragma warning restore CA1812
}
