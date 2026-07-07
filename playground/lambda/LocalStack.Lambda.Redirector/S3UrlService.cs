using Amazon.S3;
using LocalStack.Client.Options;
using Microsoft.Extensions.Options;

namespace LocalStack.Lambda.Redirector;

internal sealed class S3UrlService : IS3UrlService
{
    private readonly LocalStackOptions _localStackOptions;

    public S3UrlService(IOptions<LocalStackOptions> localStackOptions)
    {
        _localStackOptions = localStackOptions.Value;
    }

    public async Task<Uri> GetS3Url(IAmazonS3 amazonS3, string bucket, string key)
    {
        if (_localStackOptions.UseLocalStack)
        {
            return new Uri($"http://{_localStackOptions.Config.LocalStackHost}:{_localStackOptions.Config.EdgePort}/{bucket}/{key}");
        }

        var request = new Amazon.S3.Model.GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddHours(1)
        };

        var url = await amazonS3.GetPreSignedURLAsync(request).ConfigureAwait(false);
        return new Uri(url);
    }
}

internal interface IS3UrlService
{
    public Task<Uri> GetS3Url(IAmazonS3 amazonS3, string bucket, string key);
}
