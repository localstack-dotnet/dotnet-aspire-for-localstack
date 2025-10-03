// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

#pragma warning disable CA1848 // Use the LoggerMessage delegates
#pragma warning disable CA1812 // Mark members as static

using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.Messaging;
using LocalStack.Provisioning.Frontend.Models;

namespace LocalStack.Provisioning.Frontend.Handlers;

/// <summary>
/// Handles ChatMessage messages from SQS queue and persists them to DynamoDB.
/// </summary>
internal sealed class ChatMessageHandler(IAmazonDynamoDB dynamoDbClient, IConfiguration configuration, ILogger<ChatMessageHandler> logger)
    : IMessageHandler<ChatMessage>
{
    public async Task<MessageProcessStatus> HandleAsync(
        MessageEnvelope<ChatMessage> messageEnvelope,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(messageEnvelope);

        logger.LogInformation("Processing chat message: {Message} for recipient: {Recipient}",
            messageEnvelope.Message.Message,
            messageEnvelope.Message.Recipient);

        try
        {
            var tableName = configuration["AWS:Resources:ChatMessagesTableName"] ?? "ChatMessages";
            var chatMessage = messageEnvelope.Message;

            // Generate a unique message ID and timestamp
            var messageId = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Create a DynamoDB item
            var putItemRequest = new PutItemRequest
            {
                TableName = tableName,
                Item = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
                {
                    ["MessageId"] = new() { S = messageId },
                    ["Timestamp"] = new() { N = timestamp.ToString(CultureInfo.InvariantCulture) },
                    ["Message"] = new() { S = chatMessage.Message ?? string.Empty },
                    ["Recipient"] = new() { S = chatMessage.Recipient ?? "Unknown" },
                    ["ProcessedAt"] = new() { S = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
                    ["SourceQueue"] = new() { S = messageEnvelope.Source.ToString() },
                },
            };

            await dynamoDbClient.PutItemAsync(putItemRequest, token).ConfigureAwait(false);

            logger.LogInformation("Successfully stored chat message with ID {MessageId} in DynamoDB table {TableName}", messageId, tableName);

            return MessageProcessStatus.Success();
        }
        catch (AmazonDynamoDBException ex)
        {
            logger.LogError(ex, "DynamoDB error while processing chat message: {ErrorCode} - {ErrorMessage}", ex.ErrorCode, ex.Message);
            return MessageProcessStatus.Failed();
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "Chat message processing was cancelled");
            return MessageProcessStatus.Failed();
        }
    }
}
