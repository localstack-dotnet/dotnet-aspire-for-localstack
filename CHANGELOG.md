# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [9.5.3] - 2025-11-04

### Added

- **Custom Container Registry Support**: New properties on `LocalStackContainerOptions` for pulling images from private registries ([#16](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/16), [#17](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/17))
  - `ContainerRegistry` - Specify custom registry (e.g., `artifactory.company.com`, `mycompany.azurecr.io`)
  - `ContainerImage` - Custom image name (e.g., `docker-local/localstack/localstack`)
  - `ContainerImageTag` - Specific version tag (e.g., `4.10.0`)
  - All properties optional with backward-compatible defaults (`docker.io/localstack/localstack:4.10.0`)
  - Enables enterprise scenarios: Artifactory, Azure Container Registry, AWS ECR, GitHub Container Registry
  - Example: `container.ContainerRegistry = "artifactory.company.com";`
  - Closes [#16](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/16)

- **Static Port Mapping Option**: New `Port` property on `LocalStackContainerOptions` for explicit port control ([#13](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/13), [#15](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/15))
  - Fixes container recreation issue with `ContainerLifetime.Persistent`
  - Allows predictable endpoint URLs for debugging and external tool integration
  - Defaults to dynamic port for Session lifetime, static 4566 for Persistent lifetime
  - Example: `container.Port = 4566;`
  - Contributed by [@nazarii-piontko](https://github.com/nazarii-piontko) ([#15](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/15))
  - Closes [#13](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/13)

### Changed

- **LocalStack Container**: Updated from `4.9.2` → `4.10.0`
- **Aspire.Hosting.AWS**: Updated from `9.2.6` → `9.3.0`
- **SDK Centralization**: Aspire.AppHost.Sdk version now centralized in `Directory.Build.props` for easier maintenance
- **CI/CD**: Fork PR workflow improvements - test reporter now handles permission errors gracefully

### Documentation

- **Version Policy**: Added clear versioning alignment with Aspire (major.minor matching, independent patch releases)
- **Configuration Guide**: Extensive updates to `CONFIGURATION.md`
  - New "Port Configuration" section with default behavior explanations
  - New "Custom Container Registry" section with common scenarios and authentication guidance
  - Updated Container Lifetime section to reflect new defaults
  - Updated Configuration Patterns for development, CI/CD, debugging, and integration testing
- **README**: Updated with new container configuration options

### Breaking Changes

- **Default Container Lifetime Changed**: `ContainerLifetime.Session` is now the default (was `Persistent`)
  - **Impact**:
    - Containers are now cleaned up when the application stops (instead of persisting)
    - Dynamic port assignment by default (instead of static port 4566)
    - Better CI/CD experience out of the box with clean state and no port conflicts
  - **Rationale**:
    - Aligns with .NET Aspire's default convention (Session is Aspire's standard default)
    - LocalStack is a development/testing tool, not a stateful database - ephemeral behavior is more appropriate
    - Our documentation already recommended Session while defaulting to Persistent (confusing)
  - **Migration Path**:

    ```csharp
    // To restore previous Persistent behavior, explicitly set:
    builder.AddLocalStack(configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Persistent;
    });
    ```

### Contributors

We'd like to thank the following contributors for their work and feedback on this release:

- [@nazarii-piontko](https://github.com/nazarii-piontko) - Static port mapping feature ([#15](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/15))
- [@Blind-Striker](https://github.com/Blind-Striker) - Custom registry implementation, lifetime alignment, documentation updates [#17](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/17)
- [@ArturasPCodes](https://github.com/ArturasPCodes) - Feature request for custom registry support ([#16](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/16))
- [@brendonparker](https://github.com/brendonparker) - Reported persistent container issue ([#13](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/13))
- [@slang25](https://github.com/slang25) - Technical insights and feedback on port mapping and container lifetime discussions ([#13](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/13), [#15](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/15))

### Dependencies

- Aspire.Hosting: 9.5.2
- Aspire.Hosting.AWS: 9.3.0
- LocalStack.Client: 2.0.0
- LocalStack Container: 4.10.0
- .NET 8.0 and .NET 9.0

---

## [9.5.2] - 2025-10-27

### Added

- **Eager Service Loading**: New `EagerLoadedServices` property on `LocalStackContainerOptions` for pre-starting specific AWS services
  - Disables lazy loading and pre-starts configured services for faster startup
  - Updated health check to wait for eagerly-loaded services to reach 'running' state
  - Example: `container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB];`
  - Contributed by [@slang25](https://github.com/slang25) ([#7](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/7), [#8](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/8))

- **Docker Socket Access**: New `EnableDockerSocket` property on `LocalStackContainerOptions` for Lambda container support
  - Mounts `/var/run/docker.sock` to enable LocalStack Lambda container-based features
  - Security-first: opt-in with default `false` (principle of least privilege)
  - Example: `container.EnableDockerSocket = true;`
  - Closes [#11](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/11)

### Fixed

- **Lambda SQS Event Source Support**: Fixed "Invalid URL" error when Lambda functions use SQS Event Sources with LocalStack ([#6](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/6), [#9](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/9))
  - Root cause: AWS Lambda Tools process rejected LocalStack endpoints as invalid URLs
  - Solution: Auto-inject `AWS_ENDPOINT_URL` environment variable into SQS Event Source resources
  - Impact: Enables full LocalStack support for Lambda SQS Event Source triggers
  - Enhanced Lambda playground example with analytics event processing to validate the fix

### Changed

- **LocalStack Container**: Updated from `4.6.0` → `4.9.2`
- **Aspire.Hosting**: Updated to `9.5.2` (aligns with release version)
- **Aspire.Hosting.AWS**: Updated to `9.2.6` (latest stable AWS integration)
- **Aspire.Hosting.Testing**: Updated to `9.5.2`
- **Badge System**: Migrated from Gist-based storage to Badge Smith API for dynamic badge generation
- **Package Status**: Graduated from Release Candidate (RC) to stable release
- **All Dependencies**: Updated AWSSDK packages, analyzers, and third-party dependencies to latest stable versions

### Examples

- **Enhanced Lambda Playground**: Added comprehensive event-driven analytics example
  - New `AnalyzerFn` Lambda function for SQS event processing
  - Architecture: URL Shortener/Redirector → SQS Queue → Analyzer → DynamoDB (UrlAnalytics table)
  - Demonstrates: Event-driven serverless patterns, async processing, LocalStack SQS integration
  - Integration tests: `LocalStackLambdaFunctionalTests.cs`, `LocalStackLambdaResourceTests.cs`
  - Real-world use case: Analytics tracking with url_created and url_accessed events
  - See [Lambda Playground README](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda) for details

### Contributors

We'd like to thank the following contributors for their work on this release:

- [@slang25](https://github.com/slang25) - Eager service loading feature
- [@Blind-Striker](https://github.com/Blind-Striker) - Bug fixes, playground enhancements, infrastructure updates

### Breaking Changes

- None

### Dependencies

- Aspire.Hosting: 9.5.2
- Aspire.Hosting.AWS: 9.2.6
- LocalStack.Client: 2.0.0
- .NET 8.0 and .NET 9.0

---

## [9.4.0-rc.1] - 2025-08-07

### Added

#### Core Features

- **Auto-configuration support**: Single `UseLocalStack()` call automatically detects and configures all AWS resources
- **Manual configuration**: Fine-grained control with explicit `WithReference()` calls for each resource
- **LocalStack container resource**: Configurable container with session/project lifetime options, debug levels, and logging
- **Automatic AWS fallback**: When LocalStack is disabled, applications automatically work with real AWS services
- **Environment variable transfer**: Automatic configuration of service projects via standard .NET configuration hierarchy

#### AWS Service Integration

- **CloudFormation template support**: Automatic LocalStack endpoint configuration for CloudFormation resources
- **CDK stack support**: Full integration with AWS CDK stacks including automatic bootstrap handling
- **AWS SDK client support**: Works with all AWS service clients through LocalStack.NET Client integration
- **Resource provisioning**: Both `AddAWSCloudFormationTemplate`, `AddAWSCDKStack`, and `AddAWSLambdaFunction` automatically configured.

### Documentation

- **Comprehensive README**: Production-ready documentation with host and client configuration examples
- **Complete playground examples**:
  - Provisioning examples (SNS, SQS, DynamoDB) with CloudFormation & CDK approaches
  - Serverless examples (Lambda, API Gateway) with hybrid emulator integration
- **Configuration guidance**: Clear separation of host vs client configuration with code examples
- **Contributing guidelines**: Detailed contribution process and development setup instructions

### Testing & Quality

- **Unit test suite**: Comprehensive test coverage for core functionality
- **Integration tests**: Real LocalStack container testing scenarios
- **CI/CD pipeline**: Automated testing, code analysis, and package publishing
- **Code quality**: Strict analyzer rules, security scanning, and automated formatting

### Platform Support

- **.NET 8 and .NET 9**: Full support for both current LTS and latest .NET versions
- **Cross-platform**: Works on Windows, Linux, and macOS
- **LocalStack.NET Client integration**: Leverages production-tested client library (2M+ downloads)

### Examples & Samples

- **Provisioning examples**: Real-time messaging application with auto-refreshing DynamoDB viewer
- **Lambda examples**: URL shortener service with QR code generation using hybrid emulators
- **Configuration approaches**: Both auto-configuration and manual configuration patterns demonstrated
- **AWS CLI integration**: Examples for testing and verification workflows

### Initial Release Notes

This is the first Release Candidate of LocalStack.Aspire.Hosting. The package provides a complete integration between .NET Aspire and LocalStack, enabling local development and testing of AWS applications.

**Feedback Welcome:**
This RC release is feature-complete and ready for production use. Community feedback is encouraged to help finalize the API surface before the release.

### Breaking Changes

- None (initial release)

### Dependencies

- Aspire.Hosting (9.4.x)
- Aspire.Hosting.AWS (9.2.x)
- LocalStack.Client (2.x)
- .NET 8.0 and .NET 9.0

---

[9.5.3]: https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/releases/tag/9.5.3
[9.5.2]: https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/releases/tag/9.5.2
[9.4.0-rc.1]: https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/releases/tag/9.4.0-rc.1
