# .NET Aspire Integrations for LocalStack

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE) [![NuGet Version](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Fpackages%2Fnuget%2FLocalStack.Aspire.Hosting%3Fprerelease%3Dtrue)](https://www.nuget.org/packages/LocalStack.Aspire.Hosting) [![Github Packages](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Fpackages%2Fgithub%2Flocalstack-dotnet%2FLocalStack.Aspire.Hosting%3Fprerelease%3Dtrue)](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pkgs/nuget/LocalStack.Aspire.Hosting) [![CI/CD Pipeline](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/workflows/ci-cd.yml) [![Security](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/actions/workflows/github-code-scanning/codeql) [![Linux Tests](https://img.shields.io/endpoint?url=https%3A%2F%2Fapi.localstackfor.net%2Fbadges%2Ftests%2Flinux%2Flocalstack-dotnet%2Fdotnet-aspire-for-localstack%2Fmaster)](https://api.localstackfor.net/redirect/test-results/linux/localstack-dotnet/dotnet-aspire-for-localstack/master)

A .NET Aspire hosting integration for [LocalStack](https://localstack.cloud/) that enables local development and testing of cloud applications using AWS services. This package extends the official [AWS integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) to provide LocalStack-specific functionality.

## Installation

```bash
dotnet add package LocalStack.Aspire.Hosting
```

> **Package Note**: The package is named `LocalStack.Aspire.Hosting` but uses the namespace `Aspire.Hosting.LocalStack` to align with .NET Aspire hosting conventions. This ensures consistency with other Aspire hosting integrations while maintaining a unique package identity.

**Requirements**: .NET 8.0 or later (supports .NET 8, .NET 9, and .NET 10)

### Development Builds

For access to the latest features and bug fixes:

```bash
# Add GitHub Packages source
dotnet nuget add source https://nuget.pkg.github.com/localstack-dotnet/index.json \
  --name github-localstack-for-aspire \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN

# Install development packages
dotnet add package LocalStack.Aspire.Hosting --prerelease --source github-localstack-for-aspire
```

> **Note**: GitHub Packages requires a [Personal Access Token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token) with `read:packages` permission.

## Version Policy

This package follows Aspire's **major.minor** versioning but releases **patch versions** independently.

- `9.5.x` works with Aspire 9.5.x
- `9.6.x` works with Aspire 9.6.x
- `13.1.x` works with Aspire 13.1.x

We may ship features and fixes between Aspire releases. When upgrading Aspire's minor version, upgrade this package to match.

## Usage

> 💡 **Prefer learning by example?** Check out our [complete working examples](#examples) in the playground (CloudFormation, CDK, Lambda). Clone, run, explore - then come back here for configuration details.

When LocalStack is disabled in configuration, both host and client configurations automatically fall back to real AWS services without requiring code changes. The [LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) automatically switches to AWS's official client factory when LocalStack is not enabled.

### Host Configuration (AppHost)

Configure LocalStack integration in your Aspire AppHost project using auto-configuration:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// 1. Set up AWS SDK configuration (optional)
var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.USWest2);

// 2. Add LocalStack container
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

// 3. Add your AWS resources as usual
var awsResources = builder.AddAWSCloudFormationTemplate("resources", "template.yaml")
    .WithReference(awsConfig);

var project = builder.AddProject<Projects.MyService>("api")
    .WithReference(awsResources);

// 4. Auto-configure LocalStack for all AWS resources
builder.UseLocalStack(localstack);

builder.Build().Run();
```

The `UseLocalStack()` method automatically:

- Detects all AWS resources (CloudFormation, CDK stacks)
- Configures LocalStack endpoints for all AWS services and project resources
- Sets up proper dependency ordering and CDK bootstrap if needed
- Transfers LocalStack configuration to service projects via environment variables

#### Container Configuration

The `configureContainer` parameter allows you to customize LocalStack container behavior. By default, LocalStack uses lazy loading - services start only when first accessed. For faster startup in CI or when you know which services you need, configure eager loading:

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Eagerly load specific services for faster startup
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB, AwsService.S3];

    // Optional: Use Persistent lifetime for container reuse between runs
    // (Default is Session - container cleaned up when application stops)
    container.Lifetime = ContainerLifetime.Persistent;

    // Optional: Enable verbose logging for troubleshooting
    container.DebugLevel = 1;
    container.LogLevel = LocalStackLogLevel.Debug;

    // Optional: Use a specific port instead of dynamic port assignment
    container.Port = 4566;
});
```

**Available Options:**

- **`EagerLoadedServices`** - Pre-load specific AWS services at startup (reduces cold start latency)
- **`Lifetime`** - Container lifecycle: `Session` (default - cleaned up on stop) or `Persistent` (survives restarts)
- **`DebugLevel`** - LocalStack debug verbosity (0 = default, 1 = verbose)
- **`LogLevel`** - Log level control (Error, Warn, Info, Debug, Trace, etc.)
- **`Port`** - Static port mapping for LocalStack container. If not set, Session lifetime uses dynamic ports (avoids conflicts) and Persistent lifetime uses port 4566 (default LocalStack port). Set explicitly for predictable endpoint URLs
- **`ContainerRegistry`** - Custom container registry (default: `docker.io`). Use when pulling from private registries
- **`ContainerImage`** - Custom image name (default: `localstack/localstack`). Use when image is mirrored with different path
- **`ContainerImageTag`** - Custom image tag/version (default: package version). Use to pin to specific LocalStack version
- **`AdditionalEnvironmentVariables`** - Custom environment variables for advanced scenarios

For detailed configuration guide and best practices, see [Configuration Documentation](docs/CONFIGURATION.md).

#### Manual Configuration

For fine-grained control, you can manually configure each resource:

```csharp
var awsResources = builder.AddAWSCloudFormationTemplate("resources", "template.yaml")
    .WithReference(localstack)  // Manual LocalStack reference
    .WithReference(awsConfig);

var project = builder.AddProject<Projects.MyService>("api")
    .WithReference(localstack)  // Manual project reference
    .WithReference(awsResources);
```

### Client Configuration (Service Projects)

Configure AWS services in your service projects using [LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) (2M+ downloads, production-tested):

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add LocalStack configuration
builder.Services.AddLocalStack(builder.Configuration);

// Register AWS services - automatically configured for LocalStack when enabled
builder.Services.AddAwsService<IAmazonS3>();
builder.Services.AddAwsService<IAmazonDynamoDB>();
builder.Services.AddAwsService<IAmazonSNS>();

var app = builder.Build();
```

This configuration automatically detects if LocalStack is enabled and configures the AWS SDK clients accordingly. If LocalStack is not enabled, it falls back to the official AWS SDK configuration without requiring code changes.

> (Alternatively, `AddAWSServiceLocalStack` method can be used to prevent mix-up with [AddAWSService](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/net-dg-config-netcore.html).

For more details on client configuration options, see the [LocalStack.NET Client documentation](https://github.com/localstack-dotnet/localstack-dotnet-client).

### Configuration Integration

The `LocalStack.Aspire.Hosting` host automatically transfers LocalStack configuration to service projects via environment variables. This works with the standard [.NET configuration hierarchy](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0#configuration-providers): appsettings.json files -> Environment variables (can override appsettings) -> Command line arguments

**Important**: Ensure your service projects include the [EnvironmentVariablesConfigurationProvider](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-9.0#evcp) in the correct order for automatic configuration to work.

## Features

- **Auto-Configuration**: Single `UseLocalStack()` call automatically detects and configures all AWS resources
- **Manual Configuration**: Fine-grained control with explicit `WithReference()` calls for each resource
- **AWS Service Integration**: Works with CloudFormation templates, CDK stacks, and AWS service clients
- **Automatic Fallback**: Falls back to real AWS services when LocalStack is disabled
- **Container Lifecycle Management**: Configurable container with session/persistent lifetime options
- **Eager Service Loading**: Pre-load specific AWS services for faster startup in CI/CD environments
- **Extension-Based**: Works alongside official AWS integrations for .NET Aspire without code changes

## Examples

### Complete Working Examples

- **[Provisioning Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning)** - SNS, SQS, DynamoDB with CloudFormation & CDK
- **[Serverless Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda)** - Lambda functions with API Gateway and hybrid emulators

Both examples demonstrate auto-configuration and manual configuration approaches.

## What is LocalStack?

[LocalStack](https://localstack.cloud/) is a cloud service emulator that runs in a single container on your laptop or in your CI environment. It provides a fully functional local AWS cloud stack, allowing you to develop and test your cloud applications offline.

## Contributing

We welcome contributions from the community! Here's how you can get involved:

- **Try it out**: Clone the repository and test the playground examples
- **Report issues**: Share bugs or feature requests via GitHub issues
- **Submit improvements**: Pull requests for enhancements and bug fixes
- **Share feedback**: [Join discussions](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/discussions) about the implementation and roadmap

For detailed contribution guidelines, development setup, and coding standards, see our [Contributing Guide](.github/CONTRIBUTING.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes, new features, and breaking changes for each release.

## Related Projects

- [Aspire](https://github.com/dotnet/aspire) - Aspire is a unified toolchain that simplifies building, debugging, and deploying observable, production-ready distributed apps through a code-first app model.
- [AWS Integrations for .NET Aspire](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) - Official AWS integrations that this project extends
- [LocalStack .NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) - The .NET client library for LocalStack that we integrate with

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
