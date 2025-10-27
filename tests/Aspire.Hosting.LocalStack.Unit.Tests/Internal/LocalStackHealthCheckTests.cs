using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

    [Fact]
    public async Task CheckHealthAsync_Returns_Healthy_When_No_Services_Specified_And_Endpoint_Responds()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var emptyServices = ImmutableArray<string>.Empty;
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, emptyServices);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new { services = new { } });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("LocalStack is healthy", result.Description);
    }

    [Fact]
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

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("LocalStack is healthy.", result.Description);
    }

    [Fact]
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

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("dynamodb", result.Description, StringComparison.Ordinal);
        Assert.Contains("not running", result.Description, StringComparison.Ordinal);
    }

    [Fact]
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

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("s3", result.Description, StringComparison.Ordinal);
    }

    [Fact]
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

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Endpoint_Returns_Non_Success_Status()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.ServiceUnavailable, new { });

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("LocalStack is unhealthy.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns_Unhealthy_When_Services_Object_Missing()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupResponse(HttpStatusCode.OK, new { version = "1.0" }); // No services object

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("did not contain a 'services' object", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_Handles_HttpRequestException()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new HttpRequestException("Network error"));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("network error", result.Description, StringComparison.Ordinal);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_Handles_Timeout()
    {
        var healthCheckUri = new Uri("http://localhost:4566/_localstack/health");
        var services = ImmutableArray.Create("sqs");
        var healthCheck = new LocalStackHealthCheck(_httpClientFactory, healthCheckUri, services);

        _messageHandler.SetupException(new TaskCanceledException("Timeout", new TimeoutException("Operation timed out")));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("timed out", result.Description, StringComparison.Ordinal);
        Assert.NotNull(result.Exception);
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
