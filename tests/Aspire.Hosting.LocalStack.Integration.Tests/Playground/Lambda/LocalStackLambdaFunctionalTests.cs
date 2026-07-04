namespace Aspire.Hosting.LocalStack.Integration.Tests.Playground.Lambda;

/// <summary>
/// End-to-end functional tests for the Lambda playground.
/// These tests validate the complete URL shortener and analytics flow.
/// </summary>
[NotInParallel("IntegrationTests")]
[ClassDataSource<LocalStackLambdaFixture>(Shared = SharedType.PerTestSession)]
public class LocalStackLambdaFunctionalTests(LocalStackLambdaFixture fixture)
{
    /// <summary>
    /// Cached JSON serializer options for reading Lambda responses with case-insensitive property matching.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// JSON serializer options for posting requests with default PascalCase property names to match Lambda expectations.
    /// </summary>
    private static readonly JsonSerializerOptions PostJsonOptions = new()
    {
        PropertyNamingPolicy = null, // Use default PascalCase, not camelCase
    };

    [Test]
    public async Task UrlShortener_Should_Create_Short_Url(CancellationToken cancellationToken)
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        var request = new
        {
            Url = "https://aws.amazon.com"
        };

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
        await Assert.That(result.QrStatus).IsEqualTo("Pending"); // QR generation is asynchronous
        await Assert.That(result.QrPath).IsEqualTo($"/{result.Id}/qr");

        await TestOutputHelper.WriteLineAsync($"Created short URL with ID: {result.Id}");
    }

    [Test]
    public async Task UrlShortener_Should_Generate_QrCode_Asynchronously(CancellationToken cancellationToken)
    {
        // Arrange
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(90);
        var request = new
        {
            Url = "https://localstack.cloud"
        };

        // Act: creating the short URL triggers QR generation through the DynamoDB Streams event source
        var response = await httpClient.PostAsJsonAsync("/shorten", request, PostJsonOptions, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var result = JsonSerializer.Deserialize<ShortenResponse>(content, JsonOptions);
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsNotNull();
        await Assert.That(result.QrStatus).IsEqualTo("Pending");

        // Poll the QR status route until the stream processor flips it from 202 Accepted to a 302 redirect
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = false;
        using var qrClient = new HttpClient(handler);
        qrClient.BaseAddress = httpClient.BaseAddress;
        qrClient.Timeout = TimeSpan.FromSeconds(90);

        Uri? qrLocation = null;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var qrResponse = await qrClient.GetAsync(new Uri($"/{result.Id}/qr", UriKind.Relative), cancellationToken);
            if (qrResponse.StatusCode == HttpStatusCode.Found)
            {
                qrLocation = qrResponse.Headers.Location;
                break;
            }

            await Assert.That(qrResponse.StatusCode).IsEqualTo(HttpStatusCode.Accepted);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        // Assert: the redirect points at the generated object and serves a real PNG
        await Assert.That(qrLocation).IsNotNull();
        await Assert.That(qrLocation.ToString()).Contains($"/qr-bucket/qr/{result.Id}.png");

        using var pngClient = new HttpClient();
        var pngBytes = await pngClient.GetByteArrayAsync(qrLocation, cancellationToken);
        var pngMagic = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await Assert.That(pngBytes.Length).IsGreaterThan(pngMagic.Length);
        await Assert.That(pngBytes.Take(pngMagic.Length).SequenceEqual(pngMagic)).IsTrue();

        await TestOutputHelper.WriteLineAsync($"QR code ready at: {qrLocation}");
    }

    [Test]
    public async Task Redirector_Should_Redirect_To_Original_Url(CancellationToken cancellationToken)
    {
        // Arrange: First create a short URL
        using var httpClient = fixture.CreateApiGatewayClient();
        httpClient.Timeout = TimeSpan.FromSeconds(90);

        var createRequest = new
        {
            Url = "https://docs.localstack.cloud"
        };
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
        var payload = $$"""{ "Url": "{{testUrl}}" }""";
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
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>(
                StringComparer.OrdinalIgnoreCase)
            {
                [":slug"] = new()
                {
                    S = createResult.Id,
                },
                [":eventType"] = new()
                {
                    S = "url_created",
                },
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
        var createRequest = new
        {
            Url = testUrl
        };
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
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>(
                StringComparer.OrdinalIgnoreCase)
            {
                [":slug"] = new()
                {
                    S = createResult.Id,
                },
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
    private sealed record ShortenResponse(string? Id, string? QrStatus, string? QrPath);
}
