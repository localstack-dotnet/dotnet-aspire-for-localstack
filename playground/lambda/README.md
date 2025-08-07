# LocalStack Lambda Examples

This directory demonstrates serverless development with .NET Aspire using **AWS Lambda** and **API Gateway** emulators alongside LocalStack for data persistence. This example showcases the seamless integration between AWS's official emulators and LocalStack services.

## Overview

This example builds a **URL Shortener service** that leverages the best of both worlds:

- **AWS Lambda & API Gateway Emulators**: Ultra-fast local feedback loop for serverless compute, see [.NET Aspire Lambda Local Development Feature Tracker](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17) for more details and how to use them.
- **LocalStack**: Full-featured DynamoDB and S3 for data persistence and storage
- **Auto-Configuration**: Uses the recommended `UseLocalStack()` approach for automatic resource discovery

> 💡 **Configuration Approach**: This example uses the **auto-configuration feature** (`UseLocalStack()`) which is the recommended approach. For manual configuration examples with explicit `WithReference()` calls, see the [provisioning examples](../provisioning/README.md).

## Architecture

```text
┌───────────────┐     POST /shorten            ┌───────────────┐
│  API Gateway  ├─────────────────────────────▶│  ShortenFn    │
│   Emulator    │                              │   (Lambda)    │
└──────┬────────┘                              └──────┬────────┘
       │                                              │ write
 GET /{id} 302                                        │ id → URL
       ▼                                              ▼
┌───────────────┐   lookup id            ┌────────────────────┐
│  RedirectFn   ├───────────────────────▶│ DynamoDB table     │
│   (Lambda)    │                        │   (LocalStack)     │
└──────┬────────┘                        └────────────────────┘
       │ 302                                  ▲       ▲
       │                                      │       │ presign
       │                           PNG bytes  │       │ URL
       ▼                                      │       │
 User Browser ◀────── download ───────────────┘   S3 bucket
                                              (LocalStack)
```

## Resource Inventory

| Layer | Service | Local Runtime | Provisioned via |
|-------|---------|---------------|-----------------|
| **Compute & Edge** | 2 × Lambda Functions | **AWS Lambda Emulator** | `AddAWSLambdaFunction()` |
| | HTTP API Gateway | **API Gateway Emulator** | `AddAWSAPIGatewayEmulator()` |
| **Data** | DynamoDB table `Urls` | **LocalStack** | CDK Stack |
| **Storage** | S3 bucket `qr-bucket` | **LocalStack** | CDK Stack |

## Projects Structure

- **`LocalStack.Lambda.AppHost`** - Aspire orchestration with auto-configuration
- **`LocalStack.Lambda.UrlShortener`** - Lambda function for creating short URLs with QR codes
- **`LocalStack.Lambda.Redirector`** - Lambda function for redirecting short URLs to original URLs

## Quick Demo

```bash
# 1. Start the application (Docker must be running)
dotnet run --project LocalStack.Lambda.AppHost
```

**💡 Get URLs from Aspire Dashboard**: Aspire dynamically assigns host and port numbers. Open the Aspire Dashboard at `http://localhost:18888` to get the actual endpoints for:

- **APIGatewayEmulator**: For making HTTP requests to your Lambda functions
- **Lambda Test Tool**: For testing individual Lambda functions with sample payloads

### Using the API Gateway Emulator

```bash
# Get the APIGatewayEmulator base URL from Aspire Dashboard, then:

# 2. Shorten a URL with QR code
curl -d '{"url":"https://aws.amazon.com","format":"qr"}' \
     -H "Content-Type: application/json" \
     -X POST {GATEWAY_BASE_URL}/shorten
# → { "id":"abc123", "qrUrl":"http://localhost:4566/…/qr/abc123.png" }

# 3. Follow the short URL
curl -I {GATEWAY_BASE_URL}/abc123
# → 302 Found, Location: https://aws.amazon.com
```

### Using Lambda Test Tool

The [Lambda Test Tool](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17) is automatically bootstrapped and added to the Aspire Dashboard. You can:

- Test individual Lambda functions with pre-configured payloads in the `.aws-lambda-testtool/` directory in the host project.
- The debugger is automatically attached to the Lambda functions, allowing you to step through the code and inspect variables.

### CLI Testing (Optional)

For advanced testing, you can use AWS CLI commands (get LocalStack endpoint from Aspire Dashboard):

```bash
# Inspect DynamoDB table
aws dynamodb scan --table-name Urls --endpoint-url {LOCALSTACK_ENDPOINT} --region eu-central-1

# List S3 objects
aws s3api list-objects --bucket "qr-bucket" --endpoint-url {LOCALSTACK_ENDPOINT} --region eu-central-1
```

## Request Flow

> **💡 Base URL**: Get the APIGatewayEmulator base URL from the Aspire Dashboard to make requests to your Lambda functions.

1. **POST {GATEWAY_BASE_URL}/shorten**
   *Validate & slugify* → store `{ slug, originalUrl }` in DynamoDB.
   If `format=qr`, generate PNG via **[QrCodeGenerator](https://github.com/manuelbl/QrCodeGenerator)** and **[SkiaSharp](https://github.com/mono/SkiaSharp)**, upload to S3, return URL.

2. **GET {GATEWAY_BASE_URL}/{slug}**
   Lookup in DynamoDB → respond *302 Found* to the original URL.## AWS Emulator Integration

This example demonstrates how LocalStack works seamlessly with the new AWS emulators introduced in [.NET Aspire 9.x](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-1/):

- **Lambda Emulator**: Provides sub-second feedback for Lambda development
- **API Gateway Emulator**: Local HTTP API Gateway for routing
- **LocalStack Services**: DynamoDB and S3 with full AWS API compatibility

The auto-configuration feature automatically detects and configures all these resources with a single `UseLocalStack(localstack)` call.

## Development Benefits

- **Hybrid Architecture**: Best-in-class emulators for compute, LocalStack for data
- **Zero AWS Costs**: Complete local development without cloud resources
- **Fast Feedback**: Lambda changes reflect instantly via emulators
- **Production Parity**: Same AWS APIs, locally emulated
- **Auto-Configuration**: Minimal setup with automatic resource discovery

## Related Resources

- [AWS Lambda with Aspire - Part 1](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-1/)
- [AWS Lambda with Aspire - Part 2](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-2/)
- [.NET Aspire Lambda Local Development Feature Tracker](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17)
- [Manual Configuration Examples](../provisioning/README.md)
