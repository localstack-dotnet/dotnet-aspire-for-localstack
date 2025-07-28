# LocalStack Provisioning Examples

This directory contains examples demonstrating how to use the `Aspire.Hosting.LocalStack` package to develop AWS applications locally using LocalStack with .NET Aspire.

## Overview

These examples show how to build a complete AWS messaging application using:

- **SNS (Simple Notification Service)** for publishing messages
- **SQS (Simple Queue Service)** for message queuing
- **DynamoDB** for data persistence
- **LocalStack** for local AWS service emulation

The application demonstrates a typical message flow: messages are published to SNS, delivered to SQS via subscription, processed by a background service, and stored in DynamoDB.

## Projects Structure

### AppHost Projects

- **`LocalStack.Provisioning.CloudFormation.AppHost`** - Uses CloudFormation templates for AWS resource provisioning
- **`LocalStack.Provisioning.CDK.AppHost`** - Uses AWS CDK with built-in Aspire AWS integration features

### Application Projects

- **`LocalStack.Provisioning.Frontend`** - Web application demonstrating message publishing and real-time monitoring
- **`LocalStack.Provisioning.ServiceDefaults`** - Shared service defaults and configurations

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Docker Desktop
- AWS CLI (optional, for manual testing)

### Running the Examples

You can run either AppHost project depending on your preference:

#### Option 1: CloudFormation Approach

```bash
dotnet run --project LocalStack.Provisioning.CloudFormation.AppHost
```

#### Option 2: CDK Approach (Alternative)

```bash
dotnet run --project LocalStack.Provisioning.CDK.AppHost
```

Both approaches achieve the same result but use different methods for provisioning AWS resources.

## Aspire.Hosting.LocalStack Integration

The `Aspire.Hosting.LocalStack` package provides seamless integration between .NET Aspire and LocalStack, enabling local development of AWS applications.

### Key Features

- **Container Lifecycle Management**: Configurable container cleanup behavior
- **Service Configuration**: Automatic AWS SDK configuration for LocalStack endpoints
- **Resource Provisioning**: Support for CloudFormation and CDK resource provisioning
- **Development Experience**: Hot reload, logging, and debugging support

### Configuration Example

```csharp
// Configure LocalStack with custom options
var localstack = builder
    .AddLocalStack("localstack", localStackOptions, container =>
    {
        container.Lifetime = ContainerLifetime.Session;  // Keep container across debug sessions
        container.DebugLevel = 1;                        // Enable detailed logging
        container.LogLevel = LocalStackLogLevel.Debug;   // LocalStack internal logging
    });

// Add AWS resources with LocalStack integration
var awsResources = builder.AddAWSCloudFormationTemplate("AspireSampleDevResources", "app-resources.template")
    .WithParameter("DefaultVisibilityTimeout", "30")
    .WithReference(awsConfig)
    .WaitFor(localstack)           // Ensure LocalStack is ready
    .WithLocalStack(localstack);   // Configure to use LocalStack endpoints
```

### Container Lifetime Options

- **`ContainerLifetime.Session`** - Container persists across debug sessions (recommended for development)
- **`ContainerLifetime.Project`** - Container is recreated with each run

## CloudFormation AppHost Implementation

The CloudFormation approach uses traditional AWS CloudFormation templates for resource provisioning:

```csharp
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// Originally copied from https://github.com/aws/integrations-on-dotnet-aspire-for-aws
// and adjusted for Aspire.Hosting.LocalStack. All rights reserved.

using Amazon;
using Aspire.Hosting.LocalStack;

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var regionEndpoint = RegionEndpoint.USWest2;
var awsConfig = builder.AddAWSSDKConfig().WithRegion(regionEndpoint);
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

// Provision application level resources like SQS queues and SNS topics defined in the CloudFormation template file app-resources.template.
var awsResources = builder.AddAWSCloudFormationTemplate("AspireSampleDevResources", "app-resources.template")
    .WithParameter("DefaultVisibilityTimeout", "30")
    // Add the SDK configuration so the AppHost knows what account/region to provision the resources.
    .WithReference(awsConfig)
    // Wait for LocalStack container to become healthy
    .WaitFor(localstack)
    // Add the LocalStack configuration
    .WithLocalStack(localstack);

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
    .WaitFor(localstack)
    .WithReference(localstack)
    // Demonstrating binding a single output variable to an environment variable in the project.
    .WithEnvironment("ChatTopicArnEnv", awsResources.GetOutput("ChatTopicArn"))
    .WithEnvironment("ChatMessagesQueueUrlEnv", awsResources.GetOutput("ChatMessagesQueueUrl"));

await builder.Build().RunAsync().ConfigureAwait(false);
```

## Frontend Application

The Frontend application (`LocalStack.Provisioning.Frontend`) is a Blazor Server application that demonstrates:

### Message Publishing

- Interactive message composer with recipient and message fields
- Real-time form validation and submit state management
- AWS.Messaging integration for publishing to SNS topics
- Visual feedback with success/error status tracking

### Real-time Monitoring

- **DynamoDB Table Viewer**: Reusable component for real-time table monitoring
- **Auto-refresh**: Configurable polling interval (default: 2 seconds)
- **Message Count**: Live updates of total messages in the system
- **Error Handling**: Robust error handling with consecutive error tracking

### Key Components

#### MessagePublisher.razor

- Publishes messages to SNS using the AWS.Messaging library
- Demonstrates proper async/await patterns with loading states
- Integrates with the DynamoDB viewer for real-time feedback

#### DynamoDBTableViewer.razor

- Reusable component for displaying DynamoDB table data
- Supports configurable polling intervals and item limits
- Anti-flickering optimizations for smooth user experience
- Error handling with automatic retry logic

#### ChatMessageHandler.cs

- Background service that processes SQS messages
- Implements `IMessageHandler<ChatMessage>` from AWS.Messaging
- Persists processed messages to DynamoDB with proper error handling

## Message Flow Architecture

```
User Input (Frontend)
    ↓
SNS Topic (ChatTopic)
    ↓
SQS Queue (ChatMessagesQueue)
    ↓
Message Handler (ChatMessageHandler)
    ↓
DynamoDB Table (ChatMessages)
    ↓
Real-time UI Updates (DynamoDBTableViewer)
```

## AWS Resources Provisioned

- **SNS Topic**: `ChatTopic` for message publishing
- **SQS Queue**: `ChatMessagesQueue` with configurable visibility timeout
- **SNS Subscription**: Connects SNS topic to SQS queue
- **Queue Policy**: Allows SNS to send messages to SQS
- **DynamoDB Table**: `ChatMessages` with composite key (MessageId, Timestamp) and global secondary index

## Testing

### Using the Web Interface

1. Run the AppHost project
2. Navigate to the Frontend application
3. Enter a recipient and message
4. Click "Publish Message"
5. Watch real-time updates in the DynamoDB table viewer

### Using AWS CLI

For manual testing and verification, you can use the following AWS CLI commands to test the SNS→SQS→DynamoDB integration:

#### Individual Test Commands

1. **List all SNS topics**

   ```bash
   aws sns list-topics --endpoint-url http://localhost:4566 --region us-west-2
   ```

2. **List all SQS queues**

   ```bash
   aws sqs list-queues --endpoint-url http://localhost:4566 --region us-west-2
   ```

3. **Get SNS topic attributes**

   ```bash
   aws sns get-topic-attributes --topic-arn arn:aws:sns:us-west-2:000000000000:ChatTopic --endpoint-url http://localhost:4566 --region us-west-2
   ```

4. **List subscriptions for the topic**

   ```bash
   aws sns list-subscriptions-by-topic --topic-arn arn:aws:sns:us-west-2:000000000000:ChatTopic --endpoint-url http://localhost:4566 --region us-west-2
   ```

5. **Get SQS queue attributes**

   ```bash
   aws sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/ChatMessagesQueue --attribute-names All --endpoint-url http://localhost:4566 --region us-west-2
   ```

6. **Test SNS→SQS integration by publishing a test message**

   ```bash
   aws sns publish --topic-arn arn:aws:sns:us-west-2:000000000000:ChatTopic --message "Test message from CLI" --endpoint-url http://localhost:4566 --region us-west-2
   ```

7. **Receive messages from SQS to verify SNS delivery**

   ```bash
   aws sqs receive-message --queue-url http://localhost:4566/000000000000/ChatMessagesQueue --endpoint-url http://localhost:4566 --region us-west-2
   ```

8. **Check DynamoDB table**

   ```bash
   aws dynamodb list-tables --endpoint-url http://localhost:4566
   aws dynamodb scan --table-name ChatMessages --endpoint-url http://localhost:4566 --region us-west-2
   ```

## Development Benefits

- **Local Development**: No need for real AWS resources during development
- **Fast Feedback**: Instant testing without cloud deployment delays
- **Cost Effective**: No AWS charges for development and testing
- **Isolated Environment**: Each developer has their own local AWS environment
- **Debugging**: Full debugging capabilities with LocalStack logs and Aspire dashboard

## Configuration Options

### LocalStack Container Options

```csharp
container.Lifetime = ContainerLifetime.Session;     // Container persistence
container.DebugLevel = 1;                          // LocalStack debug level (0-3)
container.LogLevel = LocalStackLogLevel.Debug;     // Internal logging level
```

### DynamoDB Table Viewer Options

```razor
<DynamoDBTableViewer
    TableName="ChatMessages"
    Title="Chat Messages"
    EnablePolling="true"
    PollingIntervalMs="2000"
    MaxItems="50"
    OnItemCountChanged="HandleCountChanged" />
```

## Next Steps

- Explore the CDK AppHost implementation for alternative resource provisioning
- Extend the message flow with additional AWS services (Lambda, EventBridge, etc.)
- Add authentication and authorization using AWS Cognito
- Implement message filtering and routing logic
- Add monitoring and alerting capabilities

This example provides a solid foundation for building AWS applications locally with LocalStack and .NET Aspire, enabling rapid development and testing before deploying to production AWS environments.
