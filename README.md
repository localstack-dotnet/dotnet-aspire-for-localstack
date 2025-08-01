# .NET Aspire Integrations for LocalStack

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

This repository contains the .NET Aspire hosting integration for [LocalStack](https://localstack.cloud/), enabling local development and testing of cloud applications using AWS services. This library is designed as an extension to the official [AWS integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) and builds upon that foundation to provide LocalStack-specific functionality.

## 🚧 Work in Progress - Try It Locally

The core functionality is working and available for local testing. We've built a LocalStack integration with practical examples that you can run by cloning the repository. The project is actively evolving with **comprehensive testing suite and CI/CD pipeline development** as the next major focus, along with additional examples planned for the coming weeks.

**📅 First Preview Release**: Mid-August 2025

## What is LocalStack?

[LocalStack](https://localstack.cloud/) is a cloud service emulator that runs in a single container on your laptop or in your CI environment. It provides a fully functional local AWS cloud stack, allowing you to develop and test your cloud applications offline.

## What is LocalStack.NET Client?

[LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) is a .NET client library for LocalStack. It provides extensions and utilities to configure AWS SDK for .NET to work with LocalStack, including service registration, endpoint configuration, and development-time helpers. This Aspire integration builds upon the LocalStack.NET Client to provide container orchestration and resource management capabilities.

## What is .NET Aspire?

[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It provides a consistent, opinionated set of tools and patterns to help you build and run distributed apps.

## Aspire.Hosting.LocalStack Features

- ✅ **Extension-Only Approach**: Works alongside official [AWS
integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) with simple extension methods -
no drastic code changes required
- ✅ **Auto-Configure Mode (Still Experimental)**: Single `UseLocalStack()` call automatically detects and configures all AWS and project resources.
- ✅ **Manual Mode**: Fine-grained control with explicit `WithReference()` calls for each resource
- ✅ **LocalStack Container as Resource**: Configurable container with session/project lifetime options, debug levels, and logging configuration
- ✅ **No Additional Client Library**: Leverages existing [LocalStack.Client.Extensions](https://github.com/localstack-dotnet/localstack-dotnet-client) - no Aspire-specific client needed
- ✅ **Automatic Fallback**: When LocalStack is disabled, applications seamlessly work with real AWS services without code modifications
- ✅ **Resource Provisioning**: Both `AddAWSCloudFormationTemplate` and `AddAWSCDKStack` automatically configured for LocalStack endpoints

## Quick Start

Ready to try it out? We have **complete working examples** adapted from the official AWS Aspire integration examples:

### 🎮 Provisioning Examples

The provisioning examples includes a complete messaging flow
demonstration with real-time UI components.

**Features two configuration approaches:**

- ⚡ **Auto-Configure**: `UseLocalStack()` - Experimental but recommended
- 🔧 **Manual**: `WithReference()` - Fine-grained control

For detailed examples and configuration approaches, see the [provisioning folder README](playground/provisioning/README.md).

## What's Coming Next

### 🔄 Active Development (Next Few Weeks)

- **Comprehensive Testing Suite**: Unit and integration tests with full coverage
- **CI/CD Pipeline**: Automated testing, validation, and package publishing workflows
- **Additional Example Projects**: More scenarios and use cases
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
