using Amazon;
using Aspire.Hosting.LocalStack.Container;
using AWSCDK.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var awsConfig = builder.AddAWSSDKConfig().WithProfile("default").WithRegion(RegionEndpoint.USWest2);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

// Add the CDK Bootstrap CloudFormation template for the CDK resources to be deployed to the localstack container.
// Since LocalStack is not a real AWS account, we need to use the CDK Bootstrap CloudFormation template to deploy the CDK resources to the localstack container.
// The AddAWSCDKBootstrapCloudFormationTemplateForLocalStack method is specifically designed to work with LocalStack for security reasons.
// To prevent accidental updates to the real AWS account's CDK bootstrap stack, this method will return null if LocalStack is either not provided or not enabled.
// Comment the whole block if you use the autoconfiguration below with "builder.UseLocalStack(localstack);"
// var cdkBootstrap = builder
//     .AddAWSCDKBootstrapCloudFormationTemplateForLocalStack(localstack)

var customStack = builder
    .AddAWSCDKStack("custom", scope => new CustomStack(scope, "Aspire-custom"))
    // Add the LocalStack Reference to forward AWS calls to the LocalStack container
    // Comment "WithReference(localstack)" if you use the autoconfiguration below with "builder.UseLocalStack(localstack);"
    // .WithReference(localstack)
    // Wait for the CDK Bootstrap CloudFormation template to be deployed before deploying the custom stack
    // Comment "WaitFor(cdkBootstrap!)" if you use the autoconfiguration below with "builder.UseLocalStack(localstack);"
    // .WaitFor(cdkBootstrap!)
    .WithReference(awsConfig);

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
    // Add localstack reference to the project, it will automatically configure LocalStack.Client.Extensions
    // Comment "WithReference(localstack)" if you use the autoconfiguration below with "builder.UseLocalStack(localstack);"
    //.WithReference(localstack)
    // Add specific environment variables matching CloudFormation AppHost pattern
    .WithEnvironment("ChatTopicArnEnv", customStack.GetOutput("ChatTopicArn"))
    .WithEnvironment("ChatMessagesQueueUrlEnv", customStack.GetOutput("ChatMessagesQueueUrl"));

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
// Comment "builder.UseLocalStack(localstack);" if you use the manual configuration above for resources with "WithReference(localstack);"
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
