# LocalStack Lambda Examples

This directory demonstrates serverless development with .NET Aspire using **AWS Lambda** and **API Gateway** emulators alongside LocalStack for data persistence. This example showcases the seamless integration between AWS's official emulators and LocalStack services.

## Overview

This example builds a **URL Shortener service** that leverages the best of both worlds:

- **AWS Lambda & API Gateway Emulators**: Ultra-fast local feedback loop for serverless compute, see [.NET Aspire Lambda Local Development Feature Tracker](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17) for more details and how to use them.
- **LocalStack**: Full-featured DynamoDB and S3 for data persistence and storage
- **Auto-Configuration**: Uses the recommended `UseLocalStack()` approach for automatic resource discovery

> 💡 **Configuration Approach**: This example uses the **auto-configuration feature** (`UseLocalStack()`) which is the recommended approach. For manual configuration examples with explicit `WithReference()` calls, see the [provisioning examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning/README.md).

## Architecture

```text
┌───────────────┐     POST /shorten            ┌────────────────────┐
│  API Gateway  ├─────────────────────────────▶│ UrlShortenerFn     │
│   Emulator    │                              │    (Lambda)         │
└──────┬────────┘                              └──────────┬──────────┘
       │                                                  │ write (QrStatus=Pending)
 GET /{slug} → 302                                        │ + url_created event
       │                                                  ▼
       ▼                                        ┌────────────────────┐
┌───────────────┐   lookup slug                 │ DynamoDB (Urls)    │
│  RedirectorFn ├───────────────────────────────▶│   (LocalStack)     │
│   (Lambda)    │                                └──────────┬──────────┘
└──────┬────────┘                                           │
       │ 302 & url_accessed event                           │ DynamoDB Streams
       ▼                                                    │ (INSERT only)
 User Browser                                               ▼
                                                   ┌────────────────────┐
GET /{slug}/qr → 202 pending / 302 to QR PNG      │ QrCodeGeneratorFn  │
PNG (served by RedirectorFn as QrStatusLambda)    │     (Lambda)        │
                                                   └──────────┬──────────┘
                                                              │ PNG bytes, then
                                                              │ update QrStatus=Ready
                                                              ▼
                                                   ┌────────────────────┐
                                                   │ S3 bucket           │
                                                   │ `qr-bucket`         │
                                                   │ (LocalStack)        │
                                                   └────────────────────┘

                   Analytics Event Flow
┌─────────────────┐                ┌─────────────┐
│ UrlShortenerFn   │──url_created──▶│             │
└─────────────────┘                │ SQS Queue   │
                                   │ (LocalStack)│
┌─────────────────┐                │             │
│ RedirectorFn     │──url_accessed─▶│             │
└─────────────────┘                └──────┬──────┘
                                          │
                                          │ SQS Event Source
                                          ▼
                                   ┌─────────────┐
                                   │ AnalyzerFn  │
                                   │  (Lambda)   │
                                   └──────┬──────┘
                                          │ write
                                          ▼
                                   ┌────────────────────┐
                                   │ DynamoDB           │
                                   │ (UrlAnalytics)     │
                                   │ (LocalStack)       │
                                   └────────────────────┘
```

> **Note on tracing**: DynamoDB stream records don't propagate trace context, so `QrCodeGeneratorFn`'s activities always start as new root traces in the Aspire Dashboard rather than continuing the `POST /shorten` trace. This is a DynamoDB Streams limitation, not a bug in the sample.

## Resource Inventory

The sample runs **4 Lambda functions as 5 Lambda resources** (the `Redirector` project backs two API Gateway routes, so it is registered as both `RedirectorLambda` and `QrStatusLambda`), an API Gateway emulator, a Frontend project, LocalStack, and a CDK stack. Two independent event paths run side by side: **SQS** (analytics) and **DynamoDB Streams** (QR generation).

| Layer | Service | Local Runtime | Provisioned via |
|-------|---------|---------------|-----------------|
| **Compute & Edge** | 5 × Lambda resources: `UrlShortenerLambda`, `RedirectorLambda`, `QrStatusLambda` (same Redirector project), `AnalyzerLambda`, `QrCodeGeneratorLambda` | **AWS Lambda Emulator** | `AddAWSLambdaFunction()` |
| | HTTP API Gateway | **API Gateway Emulator** | `AddAWSAPIGatewayEmulator()` |
| **Frontend** | Command Center web UI (`LocalStack.Lambda.Frontend`) | ASP.NET Core project | `AddProject()` |
| **Data** | DynamoDB table `Urls` (Streams enabled, `NEW_IMAGE`) | **LocalStack** | CDK Stack |
| | DynamoDB table `UrlAnalytics` | **LocalStack** | CDK Stack |
| **Messaging** | SQS Queue `url-analytics-events` | **LocalStack** | CDK Stack |
| **Storage** | S3 bucket `qr-bucket` | **LocalStack** | CDK Stack |

## Projects Structure

- **`LocalStack.Lambda.AppHost`** - Aspire orchestration with auto-configuration
- **`LocalStack.Lambda.UrlShortener`** - Lambda function for creating short URLs, written to DynamoDB with `QrStatus = Pending`
- **`LocalStack.Lambda.Redirector`** - Lambda function backing two routes: `GET /{slug}` (redirect to the original URL) and `GET /{slug}/qr` (QR status/redirect, registered as the separate `QrStatusLambda` resource)
- **`LocalStack.Lambda.Analyzer`** - Lambda function for processing analytics events from SQS (demonstrates SQS Event Source with LocalStack)
- **`LocalStack.Lambda.QrCodeGenerator`** - Lambda function triggered by DynamoDB Streams `INSERT` events; renders a QR PNG, uploads it to S3, and updates the URL item with `QrStatus = Ready`
- **`LocalStack.Lambda.Frontend`** - ASP.NET Core Command Center page with a live pipeline visualization, link/analytics feeds with per-link hit counts, and a detail drawer exposing raw DynamoDB items

## Quick Demo

```bash
# 1. Start the application (Docker must be running)
dotnet run --project LocalStack.Lambda.AppHost
```

**💡 Get URLs from Aspire Dashboard**: Aspire dynamically assigns host and port numbers. Open the Aspire Dashboard at `http://localhost:18888` to get the actual endpoints for:

- **APIGatewayEmulator**: For making HTTP requests to your Lambda functions
- **Lambda Test Tool**: For testing individual Lambda functions with sample payloads
- **Frontend**: The Command Center page — the easiest way to watch the shorten → QR flow and the analytics feed update live, without hand-rolling curl commands

### Using the Command Center

Open the Frontend endpoint from the Aspire Dashboard to use the single-screen Command Center:

- **Shorten bar** posts through the API Gateway emulator and starts both background paths.
- **Live pipeline** mirrors the architecture (API Gateway → lambdas → DynamoDB/S3/SQS). Segments pulse as events flow: creating a link lights the write path, the DynamoDB Streams branch animates when a QR becomes ready, and redirect traffic lights the SQS analytics branch. Node badges show live counts.
- **Links table** shows the `Urls` records with QR status, a thumbnail once generation completes, and a hit counter per link.
- **Analytics events** lists the `AnalyzerLambda` output written from SQS events.
- **Detail drawer** opens when you click any link or event row: QR preview and actions, the link's lifecycle, and the exact raw DynamoDB attribute JSON behind the row.
- **Trace refresh requests** (in the ⚙ settings popover) is off by default so polling reads of `/api/snapshot` do not dominate Aspire traces. Turn it on when you specifically want to debug the Command Center refresh path. Polling also pauses automatically while the tab is hidden.

### Using the API Gateway Emulator

```bash
# Get the APIGatewayEmulator base URL from Aspire Dashboard, then:

# 2. Shorten a URL
curl -d '{"Url":"https://aws.amazon.com"}' \
     -H "Content-Type: application/json" \
     -X POST {GATEWAY_BASE_URL}/shorten
# → { "Id":"abc123", "QrStatus":"Pending", "QrPath":"/abc123/qr" }

# 3. Poll the QR status route until the stream processor catches up
curl -I {GATEWAY_BASE_URL}/abc123/qr
# → 202 Accepted while QrStatus is still "Pending"
# → 302 Found, Location: the LocalStack S3 object URL for the QR PNG, once QrStatus is "Ready"

# 4. Follow the short URL
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
# Inspect URLs table
aws dynamodb scan --table-name Urls --endpoint-url {LOCALSTACK_ENDPOINT} --region us-east-1

# Check analytics events
aws dynamodb scan --table-name UrlAnalytics --endpoint-url {LOCALSTACK_ENDPOINT} --region us-east-1

# List S3 objects
aws s3api list-objects --bucket "qr-bucket" --endpoint-url {LOCALSTACK_ENDPOINT} --region us-east-1

# Check SQS queue (see pending messages)
aws sqs get-queue-attributes --queue-url {ANALYTICS_QUEUE_URL} --attribute-names All --endpoint-url {LOCALSTACK_ENDPOINT} --region us-east-1
```

> **💡 Why `us-east-1`**: The AppHost pins the region to `us-east-1` because Amazon.Lambda.TestTool's bundled AWS SDK loses its signing region whenever `AWS_ENDPOINT_URL*` environment variables are set, so its DynamoDB Streams poller always signs for `us-east-1` regardless of the configured region. See `docs/plans/aws-sdk-signing-region-investigation.md` for the full investigation.

## Request Flow

> **💡 Base URL**: Get the APIGatewayEmulator base URL from the Aspire Dashboard to make requests to your Lambda functions.

1. **POST {GATEWAY_BASE_URL}/shorten**
   - *Validate & slugify* → store `{ Slug, Url, QrStatus: "Pending" }` in DynamoDB
   - Send `url_created` analytics event to SQS queue
   - Respond immediately with `{ Id, QrStatus: "Pending", QrPath }` — QR generation happens asynchronously (see step 4)

2. **GET {GATEWAY_BASE_URL}/{slug}**
   - Lookup in DynamoDB → respond *302 Found* to the original URL
   - Send `url_accessed` analytics event to SQS queue

3. **Analytics Processing (Background)**
   - Analyzer Lambda triggered by SQS Event Source
   - Processes both `url_created` and `url_accessed` events
   - Stores analytics data in UrlAnalytics DynamoDB table

4. **QR Generation (Background, DynamoDB Streams)**
   - The `Urls` table has DynamoDB Streams enabled with a new-image stream view
   - The `INSERT` from step 1 triggers `QrCodeGeneratorLambda`; `MODIFY` events (its own status update) are ignored so the write doesn't recursively re-trigger itself
   - Generates a PNG via **[QrCodeGenerator](https://github.com/manuelbl/QrCodeGenerator)** and **[SkiaSharp](https://github.com/mono/SkiaSharp)**, uploads it to S3, then updates the same item with `QrStatus: "Ready"`, `QrObjectKey`, and `QrGeneratedAt`

5. **GET {GATEWAY_BASE_URL}/{slug}/qr**
   - Unknown slug → *404*
   - `QrStatus` still `Pending` → *202 Accepted* with a small JSON status body
   - `QrStatus` is `Ready` → *302 Found* to the LocalStack S3 object URL for the PNG

## AWS Emulator Integration

This example demonstrates how LocalStack works seamlessly with the new AWS emulators introduced in [.NET Aspire 9.x](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-1/):

- **Lambda Emulator**: Provides sub-second feedback for Lambda development
- **API Gateway Emulator**: Local HTTP API Gateway for routing
- **LocalStack Services**: DynamoDB, S3, and SQS with full AWS API compatibility
- **SQS Event Source**: Demonstrates Lambda triggers from SQS queues with LocalStack
- **DynamoDB Streams Event Source**: Demonstrates change-data-capture — table writes trigger async processing without the producer publishing a second event

The auto-configuration feature automatically detects and configures all these resources with a single `UseLocalStack(localstack)` call, including the critical `AWS_ENDPOINT_URL` environment variable for the SQS and DynamoDB Streams event sources.

## Development Benefits

- **Hybrid Architecture**: Best-in-class emulators for compute, LocalStack for data and messaging
- **Zero AWS Costs**: Complete local development without cloud resources
- **Fast Feedback**: Lambda changes reflect instantly via emulators
- **Production Parity**: Same AWS APIs, locally emulated
- **Auto-Configuration**: Minimal setup with automatic resource discovery
- **Event-Driven Testing**: Test SQS and DynamoDB Streams event sources and async processing locally

## Related Resources

- [AWS Lambda with Aspire - Part 1](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-1/)
- [AWS Lambda with Aspire - Part 2](https://aws.amazon.com/blogs/developer/building-lambda-with-aspire-part-2/)
- [.NET Aspire Lambda Local Development Feature Tracker](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues/17)
- [Manual Configuration Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning/README.md)
