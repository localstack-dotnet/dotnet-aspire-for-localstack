// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

using Amazon;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var regionEndpoint = RegionEndpoint.USWest2;
var awsConfig = builder.AddAWSSDKConfig().WithProfile("default").WithRegion(regionEndpoint);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

// Provision application level resources like SQS queues and SNS topics defined in the CloudFormation template file app-resources.template.
var awsResources = builder.AddAWSCloudFormationTemplate("AspireSampleDevResources", "app-resources.template")
    .WithParameter("DefaultVisibilityTimeout", "30")
    // Add the LocalStack Reference to forward AWS calls to the LocalStack container
    // Comment "WithReference(localstack)" if you use autoconfiguration below with "builder.UseLocalStack(localstack);"
    // .WithReference(localstack)
    // Add the SDK configuration so the AppHost knows what account/region to provision the resources.
    .WithReference(awsConfig);

awsResources.WithTag("aws-repo", "integrations-on-dotnet-aspire-for-aws");

// The AWS SDK Config reference is inferred from the CloudFormation resource associated with the project. If the
// project doesn't have a CloudFormation resource, the AWS SDK Config reference can be assigned using the
// WithReference method.
builder.AddProject<Projects.LocalStack_Provisioning_Frontend>("Frontend")
    .WithExternalHttpEndpoints()
    // Demonstrating binding all the output variables to a section in IConfiguration. By default, they are bound to the AWS::Resources prefix.
    // The prefix is configurable by the optional configSection parameter.
    .WithReference(awsResources)
    // Add localstack reference to project, it will automatically configure LocalStack.Client.Extensions
    // Comment "WithReference(localstack)" if you use autoconfiguration below with "builder.UseLocalStack(localstack);"
    // .WithReference(localstack)
    // Demonstrating binding a single output variable to an environment variable in the project.
    .WithEnvironment("ChatTopicArnEnv", awsResources.GetOutput("ChatTopicArn"))
    .WithEnvironment("ChatMessagesQueueUrlEnv", awsResources.GetOutput("ChatMessagesQueueUrl"));

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
// Comment "builder.UseLocalStack(localstack);" if you use manual configuration above for resources with "WithReference(localstack);"
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);
