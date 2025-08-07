# LocalStack .NET Aspire Playground

This directory contains complete working examples demonstrating how to use `LocalStack.Aspire.Hosting` for local AWS development with .NET Aspire.

## Available Examples

### 🎮 [Provisioning Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning)

Complete messaging flow with real-time UI

Demonstrates AWS resource provisioning using CloudFormation templates and CDK stacks with LocalStack. Features both auto-configuration and manual configuration approaches.

- **Technologies**: SNS, SQS, DynamoDB, S3
- **Provisioning**: CloudFormation templates vs CDK stacks
- **Configuration**: Auto-configure vs manual reference management
- **UI**: Real-time Blazor frontend with live DynamoDB monitoring

### 🚀 [Lambda Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda)

Serverless development with hybrid emulators

Shows how LocalStack integrates with AWS Lambda and API Gateway emulators for serverless development. Builds a URL shortener service with QR code generation.

- **Technologies**: Lambda, API Gateway, DynamoDB, S3
- **Architecture**: Hybrid (AWS emulators + LocalStack services)
- **Configuration**: Auto-configure approach (recommended)
- **Demo**: Complete URL shortener with QR codes

## Choose Your Path

| If you want to learn... | Go to... |
|------------------------|----------|
| **Resource provisioning** (CloudFormation, CDK) | [Provisioning Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning) |
| **Serverless development** (Lambda, API Gateway) | [Lambda Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda) |
| **Manual configuration** examples | [Provisioning Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning) |
| **Auto-configuration** examples | Both examples use this approach |

## Prerequisites

- .NET 9.0 or later
- Docker Desktop (for LocalStack containers)
- Node.js 18+ and AWS CDK v2 (for CDK examples only)

---

**Next Steps**: Explore the individual README files in each example directory for detailed setup instructions and architectural explanations.
