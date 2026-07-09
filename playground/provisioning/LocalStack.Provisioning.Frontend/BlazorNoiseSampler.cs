using OpenTelemetry.Trace;

namespace LocalStack.Provisioning.Frontend;

internal sealed class BlazorNoiseSampler : Sampler
{
    private const string SignalRInvocationInOperationName = "Microsoft.AspNetCore.SignalR.Server.InvocationIn";
    private const string ComponentHubServiceName = "Microsoft.AspNetCore.Components.Server.ComponentHub";
    private const string RpcMethodTagName = "rpc.method";
    private const string RpcServiceTagName = "rpc.service";
    private const string OnRenderCompletedMethodName = "OnRenderCompleted";
    private const string EndInvokeJsFromDotNetMethodName = "EndInvokeJSFromDotNet";
    private const string BeginInvokeDotNetFromJsMethodName = "BeginInvokeDotNetFromJS";
    private const string ComponentAttributeNameTagName = "aspnetcore.components.attribute.name";
    private const string ComponentTypeTagName = "aspnetcore.components.type";
    private const string CodeFunctionNameTagName = "code.function.name";
    private const string PublishBarComponentTypeName = "LocalStack.Provisioning.Frontend.Components.CommandCenter.PublishBar";
    private const string OnInputAttributeName = "oninput";
    private const string CreateBinderCoreFunctionName = "<CreateBinderCore>b__0";

    private static readonly Sampler DefaultSampler = new ParentBasedSampler(new AlwaysOnSampler());

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        return IsBlazorComponentHubNoise(samplingParameters) || IsBlazorComponentEventNoise(samplingParameters)
            ? new SamplingResult(SamplingDecision.Drop)
            : DefaultSampler.ShouldSample(samplingParameters);
    }

    private static bool IsBlazorComponentHubNoise(SamplingParameters samplingParameters)
    {
        if (samplingParameters.Name != SignalRInvocationInOperationName || samplingParameters.Tags is null)
        {
            return false;
        }

        var isComponentHub = false;
        var isNoisyMethod = false;

        foreach (var tag in samplingParameters.Tags)
        {
            if (tag.Key == RpcServiceTagName && tag.Value is string serviceName)
            {
                isComponentHub = serviceName == ComponentHubServiceName;
            }
            else if (tag.Key == RpcMethodTagName && tag.Value is string methodName)
            {
                isNoisyMethod = methodName is OnRenderCompletedMethodName or EndInvokeJsFromDotNetMethodName or BeginInvokeDotNetFromJsMethodName;
            }
        }

        return isComponentHub && isNoisyMethod;
    }

    private static bool IsBlazorComponentEventNoise(SamplingParameters samplingParameters)
    {
        if (samplingParameters.Tags is null)
        {
            return false;
        }

        var isPublishBar = false;
        var isOnInput = false;
        var isInputBinder = false;

        foreach (var tag in samplingParameters.Tags)
        {
            if (tag.Key == ComponentTypeTagName && tag.Value is string componentType)
            {
                isPublishBar = componentType == PublishBarComponentTypeName;
            }
            else if (tag.Key == ComponentAttributeNameTagName && tag.Value is string attributeName)
            {
                isOnInput = attributeName == OnInputAttributeName;
            }
            else if (tag.Key == CodeFunctionNameTagName && tag.Value is string functionName)
            {
                isInputBinder = functionName == CreateBinderCoreFunctionName;
            }
        }

        return isPublishBar && isOnInput && isInputBinder;
    }
}
