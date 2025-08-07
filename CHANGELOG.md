# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

This is the first Release Candidate of Aspire.Hosting.LocalStack. The package provides a complete integration between .NET Aspire and LocalStack, enabling local development and testing of AWS applications.

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

[9.4.0-rc.1]: https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/releases/tag/9.4.0-rc.1
