namespace Aspire.Hosting.LocalStack.Unit.Tests.Internal;

public sealed class LocalStackHealthCheckTests : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TestHttpMessageHandler _messageHandler;
    private readonly HttpClient _httpClient;

    public LocalStackHealthCheckTests()
    {
        _messageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_messageHandler);
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient(Constants.LocalStackHealthClientName).Returns(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _messageHandler.Dispose();
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Healthy_When_No_Services_Specified_And_Endpoint_Responds()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var emptyServices = ImmutableArray<string>.Empty;
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, emptyServices);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new { services = new { } });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).IsEqualTo("LocalStack is healthy");
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Healthy_When_All_Services_Running()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs", "dynamodb");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new
        {
            services = new
            {
                sqs = "running",
                dynamodb = "running",
            },
        });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
        await Assert.That(result.Description).IsEqualTo("LocalStack is healthy.");
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Service_Not_Running()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs", "dynamodb");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new
        {
            services = new
            {
                sqs = "running",
                dynamodb = "starting",
            },
        });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("dynamodb");
        await Assert.That(result.Description).Contains("not running");
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Service_Missing()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs", "s3");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new
        {
            services = new
            {
                sqs = "running",
                // s3 is missing
            },
        });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("s3");
    }

    [Test]
    public async Task CheckHealthAsync_Handles_Case_Insensitive_Service_Names()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs", "dynamodb");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        // LocalStack returns uppercase service names
        _messageHandler.SetupResponse(HttpStatusCode.OK, new
        {
            services = new
            {
                SQS = "running",
                DynamoDB = "running",
            },
        });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Healthy);
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Endpoint_Returns_Non_Success_Status()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.ServiceUnavailable, new { });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).IsEqualTo("LocalStack is unhealthy.");
    }

    [Test]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Services_Object_Missing()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new { version = "1.0" }); // No services object

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("did not contain a 'services' object");
    }

    [Test]
    public async Task CheckHealthAsync_Handles_HttpRequestException()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new HttpRequestException("Network error"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("network error");
        await Assert.That(result.Exception).IsNotNull();
    }

    [Test]
    public async Task CheckHealthAsync_Handles_Timeout()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new TaskCanceledException("Timeout", new TimeoutException("Operation timed out")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("timed out");
        await Assert.That(result.Exception).IsNotNull();
    }
    [Test]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_IOException_Occurs()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new IOException("Connection closed prematurely"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.That(result.Status).IsEqualTo(HealthStatus.Unhealthy);
        await Assert.That(result.Description).Contains("starting up");
        await Assert.That(result.Exception).IsTypeOf<IOException>();
    }

    [Test]
    public async Task CheckHealthAsync_Propagates_OperationCanceledException()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new OperationCanceledException("Cancellation requested"));

        // This should NOT be caught by the generic handler anymore
        await Assert.That(async () => await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None))
            .ThrowsExactly<OperationCanceledException>();
    }


    // Custom HttpMessageHandler for testing
    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage? _response;
        private Exception? _exception;

        public void SetupResponse(HttpStatusCode statusCode, object content)
        {
            _response?.Dispose();
            var jsonContent = JsonSerializer.Serialize(content);
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json"),
            };
            _exception = null;
        }

        public void SetupException(Exception exception)
        {
            _response?.Dispose();
            _exception = exception;
            _response = null;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _exception != null
                ? throw _exception
                : Task.FromResult(_response ?? new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
