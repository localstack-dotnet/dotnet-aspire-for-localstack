using System.Diagnostics;
using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.S3;
using Amazon.S3.Model;
using LocalStack.Client.Extensions;
using LocalStack.Playground.ServiceDefaults.ActivitySources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Codecrete.QrCodeGenerator;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.QrCodeGenerator;

public class Function
{
    private const string InsertEventName = "INSERT";
    private readonly TracerProvider _traceProvider;
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IAmazonS3 _amazonS3;
    private readonly string _urlsTable;
    private readonly string _qrBucketName;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();
        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();
        builder.Services.AddAwsService<IAmazonS3>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
        _amazonDynamoDb = host.Services.GetRequiredService<IAmazonDynamoDB>();
        _amazonS3 = host.Services.GetRequiredService<IAmazonS3>();
        _urlsTable = builder.Configuration["AWS:Resources:UrlsTableName"] ?? throw new InvalidOperationException("Missing AWS:Resources:UrlsTableName");
        _qrBucketName = builder.Configuration["AWS:Resources:QrBucketName"] ?? throw new InvalidOperationException("Missing AWS:Resources:QrBucketName");
    }

    public Task FunctionHandler(DynamoDBEvent dynamoDbEvent, ILambdaContext context)
    {
        return AWSLambdaWrapper.TraceAsync(_traceProvider, async (streamEvent, lambdaContext) =>
        {
            using var activity = QrCodeGeneratorActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));
            activity?.AddTag("record.count", streamEvent.Records.Count);
            lambdaContext.Logger.LogInformation($"Processing {streamEvent.Records.Count} DynamoDB stream records");

            foreach (var record in streamEvent.Records)
            {
                if (!string.Equals(record.EventName, InsertEventName, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    await ProcessInsertAsync(record, lambdaContext).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    lambdaContext.Logger.LogError($"Failed to generate QR code from DynamoDB stream record: {ex.Message}");
                }
            }
        }, dynamoDbEvent, context);
    }

    private async Task ProcessInsertAsync(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
    {
        using var activity = QrCodeGeneratorActivitySource.ActivitySource.StartActivity(nameof(ProcessInsertAsync));
        var newImage = record.Dynamodb.NewImage;
        var slug = newImage["Slug"].S;
        var url = newImage["Url"].S;
        var key = $"qr/{slug}.png";

        activity?.AddTag("slug", slug);
        activity?.AddTag("qr.bucket", _qrBucketName);
        activity?.AddTag("qr.key", key);

        var qrCode = QrCode.EncodeText(url, QrCode.Ecc.Quartile);
        var pngData = qrCode.ToPng(scale: 10, border: 4);
        using var pngStream = new MemoryStream(pngData);

        await _amazonS3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _qrBucketName,
            Key = key,
            InputStream = pngStream,
            ContentType = "image/png",
        }).ConfigureAwait(false);

        await _amazonDynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _urlsTable,
            Key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                ["Slug"] = new() { S = slug },
            },
            UpdateExpression = "SET QrStatus = :ready, QrObjectKey = :key, QrGeneratedAt = :generatedAt",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
            {
                [":ready"] = new() { S = "Ready" },
                [":key"] = new() { S = key },
                [":generatedAt"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            },
        }).ConfigureAwait(false);

        var sanitizedSlug = slug.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        context.Logger.LogInformation($"Generated QR code for slug: {sanitizedSlug}");
    }
}
