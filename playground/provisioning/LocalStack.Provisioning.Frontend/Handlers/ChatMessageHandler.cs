// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using AWS.Messaging;
using LocalStack.Provisioning.Frontend.Models;

namespace LocalStack.Provisioning.Frontend.Handlers;

/// <summary>
/// Handles ChatMessage messages from SQS queue and persists them to DynamoDB.
/// </summary>
internal sealed partial class ChatMessageHandler(IAmazonDynamoDB dynamoDbClient, IConfiguration configuration, ILogger<ChatMessageHandler> logger)
    : IMessageHandler<ChatMessage>
{
    public async Task<MessageProcessStatus> HandleAsync(MessageEnvelope<ChatMessage> messageEnvelope, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(messageEnvelope);

        LogProcessingChatMessage(logger, messageEnvelope.Message.Message, messageEnvelope.Message.Recipient);

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

            LogStoredChatMessage(logger, messageId, tableName);

            return MessageProcessStatus.Success();
        }
        catch (AmazonDynamoDBException ex)
        {
            LogDynamoDbError(logger, ex, ex.ErrorCode, ex.Message);
            return MessageProcessStatus.Failed();
        }
        catch (OperationCanceledException ex)
        {
            LogChatMessageProcessingCancelled(logger, ex);
            return MessageProcessStatus.Failed();
        }
    }

    [LoggerMessage(
        EventId = 1,
        EventName = "ProcessingChatMessage",
        Level = LogLevel.Information,
        Message = "Processing chat message: {Message} for recipient: {Recipient}")]
    private static partial void LogProcessingChatMessage(ILogger logger, string? message, string? recipient);

    [LoggerMessage(
        EventId = 2,
        EventName = "StoredChatMessage",
        Level = LogLevel.Information,
        Message = "Successfully stored chat message with ID {MessageId} in DynamoDB table {TableName}")]
    private static partial void LogStoredChatMessage(ILogger logger, string messageId, string tableName);

    [LoggerMessage(
        EventId = 3,
        EventName = "DynamoDbError",
        Level = LogLevel.Error,
        Message = "DynamoDB error while processing chat message: {ErrorCode} - {ErrorMessage}")]
    private static partial void LogDynamoDbError(ILogger logger, Exception exception, string? errorCode, string errorMessage);

    [LoggerMessage(
        EventId = 4,
        EventName = "ChatMessageProcessingCancelled",
        Level = LogLevel.Warning,
        Message = "Chat message processing was cancelled")]
    private static partial void LogChatMessageProcessingCancelled(ILogger logger, Exception exception);
}
