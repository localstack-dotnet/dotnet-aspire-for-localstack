#pragma warning disable CA1822 // Member 'FunctionHandler' does not access instance data and can be marked as static
#pragma warning disable S2325 // Make 'FunctionHandler' a static method.

using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LocalStack.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Trace;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LocalStack.Lambda.UrlShortener;

public class Function
{
    private readonly TracerProvider _traceProvider;

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services.AddLocalStack(builder.Configuration);
        builder.Services.AddAwsService<IAmazonDynamoDB>();

        var host = builder.Build();

        _traceProvider = host.Services.GetRequiredService<TracerProvider>();
    }

    public APIGatewayHttpApiV2ProxyResponse FunctionHandler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        return AWSLambdaWrapper.Trace(_traceProvider, (proxyRequest, lambdaContext) =>
        {
            var x = (int)Convert.ChangeType(proxyRequest.PathParameters["x"], typeof(int), CultureInfo.InvariantCulture);
            var y = (int)Convert.ChangeType(proxyRequest.PathParameters["y"], typeof(int), CultureInfo.InvariantCulture);
            var sum = x + y;
            lambdaContext.Logger.LogInformation($"Adding {x} with {y} is {sum}");
            var response = new APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                    (StringComparer.Ordinal)
                    {
                        { "Content-Type", "application/json" },
                    },
                Body = sum.ToString(CultureInfo.InvariantCulture),
            };

            return response;
        }, request, context);
    }
}
