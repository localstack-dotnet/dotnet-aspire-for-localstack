namespace LocalStack.Provisioning.Frontend.Services;

/// <summary>
/// Runtime demo toggles shared between the UI and the message handler.
/// </summary>
internal sealed class DemoOptions
{
    /// <summary>
    /// When enabled the handler takes ~2 seconds per message, so bursts visibly
    /// back up in the SQS queue instead of draining within milliseconds.
    /// </summary>
    public bool SlowHandler { get; set; }
}
