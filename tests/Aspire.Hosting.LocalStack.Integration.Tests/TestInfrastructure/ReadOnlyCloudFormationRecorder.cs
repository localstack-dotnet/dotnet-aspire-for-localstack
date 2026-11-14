using System.Collections.Concurrent;
using System.Reflection;
using Amazon.CloudFormation;

namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Records import operations. DispatchProxy requires an extensible type visible to its dynamic assembly.
/// </summary>
public class ReadOnlyCloudFormationRecorder : DispatchProxy
{
    private IAmazonCloudFormation? _inner;

    internal ConcurrentQueue<string> Operations { get; } = new();

    internal static (IAmazonCloudFormation Client, ReadOnlyCloudFormationRecorder Recorder) Wrap(IAmazonCloudFormation inner)
    {
        var client = Create<IAmazonCloudFormation, ReadOnlyCloudFormationRecorder>();
        var recorder = (ReadOnlyCloudFormationRecorder)client;
        recorder._inner = inner;
        return (client, recorder);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name.EndsWith("Async", StringComparison.Ordinal))
        {
            Operations.Enqueue(targetMethod.Name);
            if (!string.Equals(targetMethod.Name, "DescribeStacksAsync", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("An imported stack must never apply infrastructure changes.");
            }
        }

        return targetMethod.Invoke(_inner ?? throw new InvalidOperationException("Recorder has no client."), args);
    }
}
