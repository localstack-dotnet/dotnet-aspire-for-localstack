namespace Aspire.Hosting.LocalStack.Integration.Tests.TestInfrastructure;

/// <summary>
/// Represents CloudFormation stack outputs.
/// </summary>
public sealed class CloudFormationStackOutputs
{
    private readonly ResourceEvent _resourceEvent;

    public CloudFormationStackOutputs(ResourceEvent resourceEvent)
    {
        _resourceEvent = resourceEvent;
    }

    public string? GetOutput(string outputName) => LocalStackTestHelpers.GetCloudFormationOutput(_resourceEvent, outputName);

    public ICloudFormationResource CloudFormationResource => (ICloudFormationResource)_resourceEvent.Resource;
}
