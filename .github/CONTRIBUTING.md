# Contributing to .NET Aspire Integrations for LocalStack

🎉 **Thank you for your interest in contributing to .NET Aspire LocalStack integration!**

We welcome contributions of all kinds - from bug reports and feature requests to code improvements and documentation updates. This guide will help you get started and ensure your contributions have the best chance of being accepted.

## 📋 Quick Reference

- 🐛 **Found a bug?** → [Create an Issue](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/new)
- 💡 **Have an idea?** → [Start a Discussion](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions)
- ❓ **Need help?** → [Q&A Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/q-a)
- 🚨 **Security issue?** → See our [Security Policy](.github/SECURITY.md)
- 🔧 **Ready to code?** → [Submit a Pull Request](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/compare)

## 🤝 Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](.github/CODE_OF_CONDUCT.md). By participating, you're expected to uphold this code. Please report unacceptable behavior to [localstack.dotnet@gmail.com](mailto:localstack.dotnet@gmail.com).

## 📝 Contributor License Agreement (CLA)

**Important**: By submitting a pull request, you agree to license your contribution under the MIT license.

## 🚀 Getting Started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (for development)
- [Docker](https://docs.docker.com/get-docker/) (for LocalStack testing)
- [Git](https://git-scm.com/downloads)
- IDE: [Visual Studio](https://visualstudio.microsoft.com/), [Rider](https://www.jetbrains.com/rider/), or [VS Code](https://code.visualstudio.com/)

### Development Environment Setup

1. **Fork and Clone**

   ```bash
   # Fork the repository on GitHub, then clone your fork
   git clone https://github.com/YOUR-USERNAME/dotnet-aspire-for-localstack.git
   cd dotnet-aspire-for-localstack

   # Add upstream remote
   git remote add upstream https://github.com/localstack-dotnet/dotnet-aspire-for-localstack.git
2. **Build the Project**

   ```bash
   # Restore dependencies
   dotnet restore

   # Build solution
   dotnet build --configuration Release
   ```

3. **Run Tests**

   This project uses [TUnit](https://tunit.dev/) as its testing framework.

   ```bash
   # Run all tests
   dotnet test

   # Run tests for a specific project
   dotnet test --project tests/Aspire.Hosting.LocalStack.Unit.Tests/Aspire.Hosting.LocalStack.Unit.Tests.csproj

   # Run specific tests (using standard filter syntax)
   dotnet test --filter "FullyQualifiedName~HealthCheck"
   ```

## 🐛 Reporting Issues

### Before Creating an Issue

1. **Search existing issues** to avoid duplicates
2. **Check [Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions)** - your question might already be answered
3. **Verify the issue** occurs with LocalStack integration in Aspire
4. **Test with latest version** when possible

### Creating a Bug Report

Use our [Issue Template](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/issues/new) which will guide you through providing:

- **Environment details** (LocalStack version, .NET Aspire version, .NET version, OS)
- **Minimal reproduction** case with Aspire AppHost
- **Expected vs actual** behavior
- **Configuration** and error messages

## 💡 Suggesting Features

We love new ideas! Here's how to suggest features:

1. **Check [existing discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/ideas)** for similar requests
2. **Start a [Discussion](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/new?category=ideas)** to gauge community interest
3. **Create an issue** if there's positive feedback and clear requirements

## 🔧 Contributing Code

### Before You Start

1. **Discuss significant changes** in [Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions) first
2. **Check for existing work** - someone might already be working on it
3. **Create an issue** if one doesn't exist (for tracking)

### Pull Request Process

1. **Create a feature branch**

   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-number-description
   ```

2. **Make your changes**
   - Follow existing code style and conventions
   - Add tests for new functionality
   - Update documentation as needed
   - Ensure all analyzers pass without warnings

3. **Test thoroughly**

   ```bash
   # Run all tests
   dotnet test
   ```

4. **Commit with conventional commits**

   ```bash
   git commit -m "feat: add support for XYZ service"
   git commit -m "fix: resolve timeout issue in DynamoDB client"
   git commit -m "docs: update installation guide"
   ```

5. **Submit the Pull Request**
   - Use our [PR Template](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/compare)
   - Provide clear description of changes
   - Link related issues

### Code Quality Standards

- ✅ **Follow existing patterns** and architectural decisions
- ✅ **Write comprehensive tests** (unit, integration, functional where applicable)
- ✅ **Add XML documentation** for public APIs
- ✅ **No analyzer warnings** - we treat warnings as errors
- ✅ **Maintain backward compatibility** (unless it's a breaking change PR)
- ✅ **Performance considerations** - avoid introducing regressions

### Testing Guidelines

We have multiple test types:

- **Unit Tests** - Fast, isolated, no external dependencies (using TUnit)
- **Integration Tests** - Test AWS Aspire integration

When adding tests:

- Place them in the appropriate test project
- Follow existing naming conventions
- Test both success and error scenarios
- Include tests for edge cases

## 📚 Documentation

- **Code comments** - Explain the "why", not the "what"
- **XML documentation** - Required for all public APIs
- **README updates** - For feature additions or breaking changes
- **CHANGELOG** - Add entries for user-facing changes

## 🔍 Review Process

1. **Automated checks** must pass (build, tests, code analysis)
2. **Maintainer review** - we aim to review within 48 hours
3. **Community feedback** - other contributors may provide input
4. **Iterative improvements** - address feedback promptly
5. **Final approval** and merge

## ❓ Getting Help

- **Questions about usage** → [Q&A Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/q-a)
- **Ideas for features** → [Ideas Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/ideas)
- **General discussion** → [General Discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/general)
- **Show your work** → [Show and Tell](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions/categories/show-and-tell)

## 🎉 Recognition

Contributors are recognized in:

- Our [Contributors](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/graphs/contributors) page
- Release notes for significant contributions
- Project documentation for major features

---

**By contributing to this project, you agree to abide by our [Code of Conduct](.github/CODE_OF_CONDUCT.md) and understand that your contributions will be licensed under the MIT License.**

Thank you for making .NET Aspire LocalStack integration better! 🚀
