namespace LocalStack.Provisioning.Frontend.Services;

internal enum MessageFlowStage
{
    Published,
    Stored,
}

internal sealed class MessageFlowEventArgs(
    MessageFlowStage stage,
    string recipient,
    string? message,
    string? messageId,
    DateTimeOffset timestamp) : EventArgs
{
    public MessageFlowStage Stage { get; } = stage;

    public string Recipient { get; } = recipient;

    public string? Message { get; } = message;

    public string? MessageId { get; } = messageId;

    public DateTimeOffset Timestamp { get; } = timestamp;
}

/// <summary>
/// In-process event hub connecting the publish action and the SQS message handler to UI components.
/// </summary>
internal sealed class MessageFlowNotifier
{
    public event EventHandler<MessageFlowEventArgs>? FlowEvent;

    public void NotifyPublished(string recipient) =>
        FlowEvent?.Invoke(this, new MessageFlowEventArgs(MessageFlowStage.Published, recipient, null, null, DateTimeOffset.UtcNow));

    public void NotifyStored(string messageId, string recipient, string message) =>
        FlowEvent?.Invoke(this, new MessageFlowEventArgs(MessageFlowStage.Stored, recipient, message, messageId, DateTimeOffset.UtcNow));
}
