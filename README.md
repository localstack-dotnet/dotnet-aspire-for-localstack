# .NET Aspire Integrations for LocalStack

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

This repository contains the .NET Aspire hosting and client integrations for [LocalStack](https://localstack.cloud/), enabling seamless local development and testing of cloud applications using AWS services.

## 🚧 Work in Progress

This project is currently under active development. The integrations are being built on top of the official [AWS integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) to provide LocalStack-specific functionality.

## What is LocalStack?

[LocalStack](https://localstack.cloud/) is a cloud service emulator that runs in a single container on your laptop or in your CI environment. It provides a fully functional local AWS cloud stack, allowing you to develop and test your cloud applications offline.

## What is .NET Aspire?

[.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview) is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It provides a consistent, opinionated set of tools and patterns to help you build and run distributed apps.

## Project Components

This repository will include:

- **Aspire.Hosting.LocalStack** - Hosting integration for LocalStack container orchestration
- **Aspire.LocalStack** - Client integration for LocalStack service configuration

## Demo & Examples

While this project is under development, you can explore a working demo that showcases .NET Aspire with LocalStack:

🔗 **[.NET OTEL Aspire LocalStack Demo](https://github.com/Blind-Striker/dotnet-otel-aspire-localstack-demo)**

This demo demonstrates how to use .NET Aspire with LocalStack for local development and observability.

## Related Projects

- [LocalStack .NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) - The official .NET client library for LocalStack
- [AWS Integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) - Official AWS integrations that this project builds upon

## Contributing

This project is in early development. Contributions, suggestions, and feedback are welcome! Please feel free to:

- Open issues for bug reports or feature requests
- Submit pull requests for improvements
- Share your use cases and requirements

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

*For updates and announcements, please watch this repository or check back regularly as we continue development.*
