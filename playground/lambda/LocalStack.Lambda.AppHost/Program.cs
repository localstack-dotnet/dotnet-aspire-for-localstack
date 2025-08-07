#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.

using Amazon;
using Aspire.Hosting.AWS.Lambda;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

var addFunction = builder
    .AddAWSLambdaFunction<Projects.LocalStack_Lambda_UrlShortener>(
        name: "AddFunction",
        lambdaHandler: "LocalStack.Lambda.UrlShortener::LocalStack.Lambda.UrlShortener.Function::FunctionHandler")
    .WithReference(awsConfig);

builder.AddAWSAPIGatewayEmulator("APIGatewayEmulator", APIGatewayType.HttpV2)
    // Add the Web API calculator routes
    .WithReference(addFunction, Method.Get, "/add/{x}/{y}");

// var customStack = builder
//     .AddAWSCDKStack("custom", scope => new CustomStack(scope, "Aspire-custom"))
//     .WithReference(awsConfig);

// Add outputs for all the resources to make them available to the frontend
// customStack.AddOutput("BucketName", stack => stack.Bucket.BucketName);
// customStack.AddOutput("ChatTopicArn", stack => stack.ChatTopic.TopicArn);
// customStack.AddOutput("ChatMessagesQueueUrl", stack => stack.ChatMessagesQueue.QueueUrl);
// customStack.AddOutput("ChatMessagesTableName", stack => stack.ChatMessagesTable.TableName);

// customStack.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
