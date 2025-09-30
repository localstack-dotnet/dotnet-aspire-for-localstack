// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace AWSCDK.AppHost;

internal sealed class UrlShortenerStack : Stack
{
    public ITable UrlsTable { get; }
    public IBucket QrBucket { get; }
    public IQueue AnalyticsQueue { get; }
    public ITable AnalyticsTable { get; }

    public UrlShortenerStack(Construct scope, string id) : base(scope, id)
    {
        UrlsTable = new Table(this, "UrlsTable", new TableProps
        {
            TableName = "Urls",
            PartitionKey = new Attribute { Name = "Slug", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
        });

        QrBucket = new Bucket(this, "QrBucket", new BucketProps
        {
            BucketName = "qr-bucket",
        });

        AnalyticsQueue = new Queue(this, "AnalyticsQueue", new QueueProps
        {
            QueueName = "url-analytics-events",
            VisibilityTimeout = Duration.Seconds(30),
        });

        AnalyticsTable = new Table(this, "AnalyticsTable", new TableProps
        {
            TableName = "UrlAnalytics",
            PartitionKey = new Attribute { Name = "EventId", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "Timestamp", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
        });
    }
}
