# LocalStack Provisioning Examples

This directory contains examples demonstrating how to use the `Aspire.Hosting.LocalStack` package to develop AWS applications locally using LocalStack with .NET Aspire.

## Overview

These examples are **adapted from the official [AWS Aspire integration examples](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/tree/main/playground/CloudFormationProvisioning)** with minimal changes to work with LocalStack. They demonstrate how to easily migrate existing AWS Aspire applications to use LocalStack for local development.

**New Features Added to the official AWS Aspire integration examples:**

- **Interactive Frontend**: Real-time messaging application with auto-refreshing DynamoDB viewer
- **Complete Message Flow**: SNS → SQS → DynamoDB → Live UI updates
- **Two Configuration Approaches**: Manual reference management vs automatic configuration

The application demonstrates a typical AWS messaging flow: messages are published to SNS, delivered to SQS via subscription, processed by a background service, and stored in DynamoDB with real-time UI monitoring.

## Projects Structure

### AppHost Projects (Provisioning Methods)

Both projects create **identical AWS resources** but use different provisioning approaches:

- **`LocalStack.Provisioning.CloudFormation.AppHost`** - Uses [CloudFormation template](./LocalStack.Provisioning.CloudFormation.AppHost/app-resources.template) for AWS resource provisioning
- **`LocalStack.Provisioning.CDK.AppHost`** - Uses [AWS CDK Stack](./LocalStack.Provisioning.CDK.AppHost/CustomStack.cs) for AWS resource provisioning

### Application Projects

- **`LocalStack.Provisioning.Frontend`** - Blazor application with real-time messaging and DynamoDB monitoring
- **`LocalStack.Provisioning.ServiceDefaults`** - Shared service defaults and configurations

**Note**: The Frontend application behaves identically regardless of which AppHost you use.

## Quick Comparison

| Aspect | CloudFormation AppHost | CDK AppHost |
|--------|----------------------|-------------|
| **Provisioning** | [JSON Template](./LocalStack.Provisioning.CloudFormation.AppHost/app-resources.template) | [C# CDK Stack](./LocalStack.Provisioning.CDK.AppHost/CustomStack.cs) |
| **Configuration** | See [Program.cs](./LocalStack.Provisioning.CloudFormation.AppHost/Program.cs) | See [Program.cs](./LocalStack.Provisioning.CDK.AppHost/Program.cs) |
| **Resources Created** | SNS, SQS, DynamoDB | SNS, SQS, DynamoDB, S3 |
| **Frontend Behavior** | Identical | Identical |
| **Learning Focus** | CloudFormation templates | CDK programmatic approach |

**🔍 Explore the Code**: Check the `Program.cs` files to see manual vs auto-configure examples with detailed comments.

## Configuration Approaches

This integration provides **two ways** to configure LocalStack with your AWS resources:

### ⚡ Auto-Configure Approach (Experimental - Recommended)

**Single method configures everything automatically**

```csharp
// 1. Add LocalStack container
var localstack = builder.AddLocalStack(awsConfig: awsConfig, configureContainer: container => {
    container.Lifetime = ContainerLifetime.Session;
    container.LogLevel = LocalStackLogLevel.Debug;
});

// 2. Add your AWS resources normally
var awsResources = builder.AddAWSCloudFormationTemplate("resources", "app-resources.template")
    .WithReference(awsConfig);

// 3. Auto-configure everything with one call
builder.UseLocalStack(localstack);  // 🪄 Automatically detects and configures all AWS resources
```

### 🔧 Manual Approach (Fine-grained Control)

**Explicit reference management for each resource**

```csharp
// Manual LocalStack references (currently commented out in Program.cs files)
var awsResources = builder.AddAWSCloudFormationTemplate("resources", "app-resources.template")
    .WithReference(localstack);  // Manual LocalStack reference

var project = builder.AddProject<Projects.Frontend>("Frontend")
    .WithReference(localstack);  // Manual project reference
```

**💡 Hands-on Learning**: Both `Program.cs` files contain commented code blocks. You can easily switch between manual and auto approaches by commenting/uncommenting the relevant sections.

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Docker Desktop
- AWS CLI (optional, for manual testing)

### Running the Examples

Choose your preferred provisioning method:

#### Option 1: CloudFormation Template Approach

```bash
dotnet run --project LocalStack.Provisioning.CloudFormation.AppHost
```

👀 **Explore**: [Program.cs](./LocalStack.Provisioning.CloudFormation.AppHost/Program.cs) | [Template](./LocalStack.Provisioning.CloudFormation.AppHost/app-resources.template)

#### Option 2: AWS CDK Approach

```bash
dotnet run --project LocalStack.Provisioning.CDK.AppHost
```

👀 **Explore**: [Program.cs](./LocalStack.Provisioning.CDK.AppHost/Program.cs) | [CDK Stack](./LocalStack.Provisioning.CDK.AppHost/CustomStack.cs)

Both create the same messaging infrastructure and provide identical frontend functionality.

## Aspire.Hosting.LocalStack Integration

The `Aspire.Hosting.LocalStack` package provides seamless integration between .NET Aspire and LocalStack, enabling local development of AWS applications.

### Key Features

- **Automatic Resource Detection**: Auto-configure all AWS resources with `UseLocalStack()`
- **Manual vs Auto-Configure**: Choice between fine-grained control and convention-based setup
- **Container Lifecycle Management**: Configurable container cleanup behavior
- **Interface-Based Design**: Better type safety and abstraction with `ILocalStackResource`
- **Service Configuration**: Automatic AWS SDK configuration for LocalStack endpoints
- **Resource Provisioning**: Support for CloudFormation and CDK resource provisioning
- **Bidirectional Tracking**: Annotation system for resource relationship visibility
- **Development Experience**: Hot reload, logging, and debugging support

### Switching Between Configuration Approaches

Both `Program.cs` files are currently configured for **auto-configure mode**. To experiment with manual configuration:

#### In CloudFormation AppHost

```csharp
// Comment out auto-configure
// builder.UseLocalStack(localstack);

// Uncomment manual references
// .WithReference(localstack)
```

#### In CDK AppHost

```csharp
// Comment out auto-configure
// builder.UseLocalStack(localstack);

// Uncomment manual CDK bootstrap and references
// var cdkBootstrap = builder.AddAWSCDKBootstrapCloudFormationTemplate()...
// .WithReference(localstack)
// .WaitFor(cdkBootstrap)
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

Both provisioning approaches create these resources:

- **SNS Topic**: `ChatTopic` for message publishing
- **SQS Queue**: `ChatMessagesQueue` with configurable visibility timeout
- **SNS Subscription**: Connects SNS topic to SQS queue
- **Queue Policy**: Allows SNS to send messages to SQS
- **DynamoDB Table**: `ChatMessages` with composite key (MessageId, Timestamp) and global secondary index
- **S3 Bucket**: (CDK approach only) Additional storage demonstration

## Testing

### Using the Web Interface

1. Run either AppHost project
2. Navigate to the Frontend application in Aspire Dashboard
3. Enter a recipient and message
4. Click "Publish Message"
5. Watch real-time updates in the DynamoDB table viewer

### Using AWS CLI

For manual testing and verification, you can use AWS CLI commands to test the SNS→SQS→DynamoDB integration:

#### Quick Test Commands

1. **List all resources**

   ```bash
   aws sns list-topics --endpoint-url http://localhost.localstack.cloud:4566 --region us-west-2
   aws sqs list-queues --endpoint-url http://localhost.localstack.cloud:4566 --region us-west-2
   aws dynamodb list-tables --endpoint-url http://localhost.localstack.cloud:4566 --region us-west-2
   ```

2. **Test message flow**

   ```bash
   # Publish to SNS (replace <topic_arn> with actual ARN from Aspire dashboard)
   aws sns publish --topic-arn <topic_arn> --message "Test message from CLI" --endpoint-url http://localhost.localstack.cloud:4566 --region us-west-2

   # Check DynamoDB for processed message
   aws dynamodb scan --table-name ChatMessages --endpoint-url http://localhost.localstack.cloud:4566 --region us-west-2
   ```

## Development Benefits

- **Local Development**: No need for real AWS resources during development
- **Fast Feedback**: Instant testing without cloud deployment delays
- **Cost Effective**: No AWS charges for development and testing
- **Isolated Environment**: Each developer has their own local AWS environment
- **Easy Migration**: Minimal changes from existing AWS Aspire applications
- **Debugging**: Full debugging capabilities with LocalStack logs and Aspire dashboard
