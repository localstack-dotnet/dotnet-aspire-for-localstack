// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

#pragma warning disable IDE0130
// ReSharper disable CheckNamespace

using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace AWSCDK.AppHost;

internal sealed class CustomStack : Stack
{
    public IBucket Bucket { get; }

    public ITopic ChatTopic { get; }

    public IQueue ChatMessagesQueue { get; }

    public ITable ChatMessagesTable { get; }

    public CustomStack(Construct scope, string id)
        : base(scope, id)
    {
        // Keep bucket for potential future demonstration
        Bucket = new Bucket(this, "Bucket");

        ChatTopic = new Topic(this, "ChatTopic");

        ChatMessagesQueue = new Queue(this, "ChatMessagesQueue", new QueueProps
        {
            VisibilityTimeout = Duration.Seconds(30),
        });

        ChatTopic.AddSubscription(new SqsSubscription(ChatMessagesQueue));

        var chatMessagesTable = new Table(this, "ChatMessagesTable", new TableProps
        {
            TableName = "ChatMessages",
            PartitionKey = new Attribute { Name = "MessageId", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "Timestamp", Type = AttributeType.NUMBER },
            BillingMode = BillingMode.PAY_PER_REQUEST,
        });

        chatMessagesTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
        {
            IndexName = "TimestampIndex",
            PartitionKey = new Attribute { Name = "Timestamp", Type = AttributeType.NUMBER },
            ProjectionType = ProjectionType.ALL,
        });

        ChatMessagesTable = chatMessagesTable;
    }
}
