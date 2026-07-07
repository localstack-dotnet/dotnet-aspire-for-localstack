#if NET10_0 // Redirector project reference is net10.0-only; tests excluded on net8.0/net9.0 TFMs
using Amazon.S3;
using Amazon.S3.Model;
using Aspire.Hosting.LocalStack.Configuration;
using LocalStack.Lambda.Redirector;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.LocalStack.Unit.Tests.Playground;

public class S3UrlServiceTests
{
    [Test]
    public async Task GetS3Url_WhenUseLocalStack_ReturnsDirectUrl()
    {
        // Arrange
        ILocalStackOptions localStackOptions = new LocalStackOptions();
        localStackOptions = localStackOptions.WithUseLocalStack(true);
        var options = Options.Create((LocalStackOptions)localStackOptions);
        var s3Client = Substitute.For<IAmazonS3>();
        var service = new S3UrlService(options);

        // Act
        var result = await service.GetS3UrlAsync(s3Client, "test-bucket", "test-key");

        // Assert
        await Assert.That(result.ToString()).Contains("test-bucket/test-key");
        await s3Client.DidNotReceive().GetPreSignedURLAsync(Arg.Any<GetPreSignedUrlRequest>());
    }

    [Test]
    public async Task GetS3Url_WhenNotUseLocalStack_ReturnsPresignedUrl()
    {
        // Arrange
        ILocalStackOptions localStackOptions = new LocalStackOptions();
        localStackOptions = localStackOptions.WithUseLocalStack(false);
        var options = Options.Create((LocalStackOptions)localStackOptions);
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client.GetPreSignedURLAsync(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("https://test-bucket.s3.amazonaws.com/test-key?signature=abc123");
        var service = new S3UrlService(options);

        // Act
        var result = await service.GetS3UrlAsync(s3Client, "test-bucket", "test-key");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("https://test-bucket.s3.amazonaws.com/test-key?signature=abc123");
    }
}
#endif
