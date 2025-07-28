# .NET Aspire Integrations for LocalStack

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

This repository contains the .NET Aspire hosting integration for [LocalStack](https://localstack.cloud/), enabling local development and testing of cloud applications using AWS services. This library is designed as an extension to the official [AWS integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) and builds upon that foundation to provide LocalStack-specific functionality.

## 🚧 Work in Progress - Try It Locally

The core functionality is working and available for local testing. We've built a LocalStack integration with practical examples that you can run by cloning the repository. The project is actively evolving with testing, additional examples, and CI/CD pipeline development planned for the coming weeks.

**📅 First Preview Release**: Mid-August 2025

## What is LocalStack?

[LocalStack](https://localstack.cloud/) is a cloud service emulator that runs in a single container on your laptop or in your CI environment. It provides a fully functional local AWS cloud stack, allowing you to develop and test your cloud applications offline.

## What is LocalStack.NET Client?

[LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) is a .NET client library for LocalStack. It provides extensions and utilities to configure AWS SDK for .NET to work with LocalStack, including service registration, endpoint configuration, and development-time helpers. This Aspire integration builds upon the LocalStack.NET Client to provide container orchestration and resource management capabilities.

## What is .NET Aspire?

[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It provides a consistent, opinionated set of tools and patterns to help you build and run distributed apps.

## Current Features

### Aspire.Hosting.LocalStack

- ✅ **LocalStack Container as Resource**: Configurable container with session/project lifetime options, debug levels, and logging configuration
- ✅ **No Additional Client Library**: Leverages existing [LocalStack.Client.Extensions](https://github.com/localstack-dotnet/localstack-dotnet-client) - no Aspire-specific client needed
- ✅ **Extension-Only Approach**: Works alongside official [AWS integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) with simple extension methods - no drastic code changes required
- ✅ **Automatic Fallback**: When LocalStack is disabled, applications seamlessly work with real AWS services without code modifications
- ✅ **Resource Provisioning**: Both `AddAWSCloudFormationTemplate` and `AddAWSCDKStack` from AWS Aspire library automatically configured for LocalStack endpoints during development

## Quick Start

Ready to try it out? Check out our examples:

### 🎮 Playground Examples

```bash
# Clone the repository
git clone https://github.com/localstack-dotnet/dotnet-aspire-for-localstack.git
cd dotnet-aspire-for-localstack

# Run the SNS→SQS→DynamoDB messaging example
dotnet run --project playground/provisioning/LocalStack.Provisioning.CloudFormation.AppHost
```

The playground includes a complete messaging flow demonstration with real-time UI components. For detailed information about the examples and how to use them, see the [playground README](playground/provisioning/README.md).

## What's Coming Next

### 🔄 Active Development (Next Few Weeks)

- **Testing Suite**: Unit and integration tests
- **Additional Example Projects**: More scenarios and use cases
- **CI/CD Pipeline**: Automated testing and package publishing
- **Documentation**: Complete API documentation and guides

### 📦 First Preview Release: Mid-August 2025

- NuGet package publication
- Stable API surface
- Production-ready documentation

## Contributing

We welcome contributions from the community! Here's how you can get involved:

### 🧪 Try It Out

- Clone the repository and test the playground examples
- Report bugs or issues you encounter
- Share your use cases and requirements

### 🔨 Contribute Code

- Submit pull requests for improvements
- Help with documentation and examples
- Add support for additional AWS services

### 💬 Join the Discussion

- Open issues for feature requests
- Share feedback on the current implementation
- Propose architectural improvements

## Related Projects

- [AWS Integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) - Official AWS integrations that this project extends
- [LocalStack .NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) - The .NET client library for LocalStack that we integrate with

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

*⭐ Star this repository to stay updated on our progress toward the mid-August preview release!*
