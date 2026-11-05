using System.Globalization;
using Amazon.S3;
using LocalStack.Client;
using LocalStack.Client.Options;

var expectFailure = string.Equals(Environment.GetEnvironmentVariable("EXPECTED_FAILURE"), "true", StringComparison.Ordinal);
try
{
    await VerifyEndpointAsync().ConfigureAwait(false);
    if (expectFailure)
    {
        throw new InvalidOperationException("The wrong-target probe unexpectedly succeeded.");
    }

    return 0;
}
catch (Exception error) when (expectFailure && error is HttpRequestException or Amazon.Runtime.AmazonClientException or Amazon.Runtime.AmazonServiceException)
{
    return 23;
}

static async Task VerifyEndpointAsync()
{
    var enabled = bool.Parse(Required("LocalStack__UseLocalStack"));
    if (!enabled)
    {
        throw new InvalidOperationException("The endpoint probe requires an explicit LocalStack backend.");
    }

    var session = SessionStandalone.Init()
        .WithSessionOptions(new SessionOptions(
            Required("LocalStack__Session__AwsAccessKeyId"), Required("LocalStack__Session__AwsAccessKey"),
            Required("LocalStack__Session__AwsSessionToken"), Required("LocalStack__Session__RegionName")))
        .WithConfigurationOptions(new ConfigOptions(
            Required("LocalStack__Config__LocalStackHost"),
            bool.Parse(Required("LocalStack__Config__UseSsl")),
            bool.Parse(Required("LocalStack__Config__UseLegacyPorts")),
            int.Parse(Required("LocalStack__Config__EdgePort"), CultureInfo.InvariantCulture)))
        .Create();
    using var client = session.CreateClientByImplementation<AmazonS3Client>();
    ((AmazonS3Config)client.Config).MaxErrorRetry = 0;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var response = await client.ListBucketsAsync(timeout.Token).ConfigureAwait(false);
    if (response.HttpStatusCode != System.Net.HttpStatusCode.OK
        || !response.Buckets.Any(bucket => bucket.BucketName == Required("EXPECTED_BUCKET")))
    {
        throw new InvalidOperationException("The endpoint probe did not reach the seeded LocalStack bucket.");
    }
}

static string Required(string name) => Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException("A required endpoint probe setting is absent.");
