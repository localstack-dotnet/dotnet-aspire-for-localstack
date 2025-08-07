// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.S3;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace AWSCDK.AppHost;

internal sealed class UrlShortenerStack : Stack
{
    public ITable UrlsTable { get; }
    public IBucket QrBucket { get; }

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
    }
}
