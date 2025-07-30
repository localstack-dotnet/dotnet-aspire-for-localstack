using Amazon;
using Aspire.Hosting.LocalStack;
using AWSCDK.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var regionEndpoint = RegionEndpoint.USWest2;
var awsConfig = builder.AddAWSSDKConfig().WithProfile("default").WithRegion(regionEndpoint);
// Set up a configuration for the LocalStack
var localStackOptions = builder.AddLocalStackConfig().WithRegion(regionEndpoint.SystemName);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack("localstack", localStackOptions, container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });


var cdkBootstrapTemplate = Path.Combine(Directory.GetCurrentDirectory(), "cdk-bootstrap.template");

if (!File.Exists(cdkBootstrapTemplate))
{
    throw new InvalidOperationException($"Could not find `{cdkBootstrapTemplate}` template.");
}

// Bootstrap the CDK environment against LocalStack.
var cdkBootstrap = builder.AddAWSCloudFormationTemplate("CDKBootstrap", cdkBootstrapTemplate)
    .WithLocalStack(localstack);

var customStack = builder
    .AddAWSCDKStack("custom", scope => new CustomStack(scope, "Aspire-custom"))
    .WithReference(awsConfig)
    .WaitFor(cdkBootstrap)
    .WithLocalStack(localstack);

// Add outputs for all the resources to make them available to the frontend
customStack.AddOutput("BucketName", stack => stack.Bucket.BucketName);
customStack.AddOutput("ChatTopicArn", stack => stack.ChatTopic.TopicArn);
customStack.AddOutput("ChatMessagesQueueUrl", stack => stack.ChatMessagesQueue.QueueUrl);
customStack.AddOutput("ChatMessagesTableName", stack => stack.ChatMessagesTable.TableName);

customStack.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

builder.AddProject<Projects.LocalStack_Provisioning_Frontend>("Frontend")
    .WithExternalHttpEndpoints()
    // Reference all outputs from the custom stack (similar to CloudFormation approach)
    .WithReference(customStack)
    // Add localstack reference to project, it will automatically configure LocalStack.Client.Extensions
    .WithReference(localstack)
    // Add specific environment variables matching CloudFormation AppHost pattern
    .WithEnvironment("ChatTopicArnEnv", customStack.GetOutput("ChatTopicArn"))
    .WithEnvironment("ChatMessagesQueueUrlEnv", customStack.GetOutput("ChatMessagesQueueUrl"));

await builder.Build().RunAsync().ConfigureAwait(false);
