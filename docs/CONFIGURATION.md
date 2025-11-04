# LocalStack Configuration Guide

This guide covers configuration options for customizing LocalStack container behavior in .NET Aspire applications.

## Quick Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EagerLoadedServices` | `IReadOnlyCollection<AwsService>` | `[]` (empty) | AWS services to pre-load at container startup |
| `Lifetime` | `ContainerLifetime` | `Session` | Container lifecycle behavior |
| `Port` | `int?` | `null` | Static port mapping for LocalStack container |
| `ContainerRegistry` | `string?` | `null` (`docker.io`) | Custom container registry |
| `ContainerImage` | `string?` | `null` (`localstack/localstack`) | Custom container image name |
| `ContainerImageTag` | `string?` | `null` (package version) | Custom container image tag/version |
| `EnableDockerSocket` | `bool` | `false` | Mount Docker socket for Lambda support |
| `DebugLevel` | `int` | `0` | LocalStack DEBUG flag (0 or 1) |
| `LogLevel` | `LocalStackLogLevel` | `Error` | LocalStack LS_LOG level |
| `AdditionalEnvironmentVariables` | `IDictionary<string, string>` | `{}` (empty) | Custom environment variables |

## Service Loading Strategy

LocalStack supports two service loading strategies via the [`EAGER_SERVICE_LOADING`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) configuration.

### Lazy Loading (Default)

Services start only when first accessed. This provides faster initial container startup but adds latency to the first request that uses each service.

```csharp
var localstack = builder.AddLocalStack(); // No eager loading - uses lazy loading
```

**When to use:**

- Local development (faster container startup)
- Exploratory development where service usage isn't predictable
- When working with many services but only using a few

### Eager Loading

Pre-loads specific AWS services during container startup. The container takes longer to start but subsequent requests have no cold-start latency.

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB, AwsService.S3];
});
```

This sets the [`EAGER_SERVICE_LOADING=1`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) and [`SERVICES`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) environment variables automatically.

**When to use:**

- CI/CD pipelines (consistent startup, no cold-start flakiness)
- Integration tests (eliminate service initialization variance)
- When you know exactly which services you'll use

**Available Services:**

You can eagerly load any AWS service supported by LocalStack. Common examples:

```csharp
container.EagerLoadedServices =
[
    AwsService.Sqs,           // Simple Queue Service
    AwsService.DynamoDB,      // DynamoDB
    AwsService.S3,            // S3 Storage
    AwsService.Sns,           // Simple Notification Service
    AwsService.Lambda,        // Lambda Functions
    AwsService.CloudFormation,// CloudFormation
    AwsService.SecretsManager,// Secrets Manager
    AwsService.Ssm,           // Systems Manager (Parameter Store)
    // ... see LocalStack docs for full list
];
```

For a complete list of supported services, check the [LocalStack health endpoint](https://docs.localstack.cloud/aws/capabilities/networking/internal-endpoints/#localstack-endpoints) at `/_localstack/health`.

## Container Lifetime

Controls when the LocalStack container is created and destroyed.

### Session (Default - Recommended)

Container is created when the application starts and destroyed when it stops. This is the default lifetime and aligns with Aspire's conventions.

```csharp
// Default - no need to set explicitly
container.Lifetime = ContainerLifetime.Session;
```

**When to use:**

- CI/CD pipelines (clean slate for each run)
- Integration tests (isolated test runs)
- When you want guaranteed clean state
- Most development scenarios

### Persistent

Container survives application restarts. Data may persist between debugging sessions depending on your configuration.

```csharp
container.Lifetime = ContainerLifetime.Persistent;
```

**When to use:**

- Local development when you want container reuse between runs
- When combined with [LocalStack persistence](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/)
- When working with large infrastructure that's slow to provision

## Port Configuration

Controls how LocalStack's port is mapped from the container to the host machine.

### Default Behavior

By default, port mapping depends on container lifetime:

```csharp
// Session lifetime (Default) - uses dynamic port assignment
container.Lifetime = ContainerLifetime.Session;
// Port will be: random available port

// Persistent lifetime - uses default LocalStack port (4566)
container.Lifetime = ContainerLifetime.Persistent;
// Port will be: 4566
```

### Static Port Mapping

You can explicitly specify a port to ensure predictable endpoint URLs:

```csharp
container.Port = 4566; // Always use port 4566
```

### When to Use Static Ports

**Use static ports when:**

- You need predictable endpoint URLs across runs
- External tools need to connect to a known port
- You're integrating with legacy systems expecting specific ports
- Debugging network issues and need consistency

**Use dynamic ports when:**

- Running multiple instances simultaneously (tests, parallel development)
- Avoiding port conflicts with other services
- In CI/CD environments with parallel builds

### Port Conflict Resolution

If you encounter port conflicts:

```csharp
// Option 1: Use a different static port
container.Port = 4567;

// Option 2: Switch to Session lifetime for dynamic port assignment
container.Lifetime = ContainerLifetime.Session;
// Dynamic ports are used automatically when Lifetime = Session and Port is not set
```

## Custom Container Registry

Configure LocalStack to pull from private registries or container mirrors.

### Why Use Custom Registries?

Organizations often need to pull images from:

- **Private registries** (Artifactory, Harbor) for compliance/security
- **Container mirrors** to avoid Docker Hub rate limits
- **Internal registries** (Azure Container Registry, AWS ECR) for air-gapped environments
- **Custom builds** with organization-specific configurations

### Configuration Properties

Three properties work together to specify the complete image location:

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.ContainerRegistry = "artifactory.company.com";  // Where to pull from
    container.ContainerImage = "docker-mirrors/localstack/localstack";  // Image path
    container.ContainerImageTag = "4.10.0";  // Specific version
});
```

**Defaults:**

- `ContainerRegistry`: `docker.io` (Docker Hub)
- `ContainerImage`: `localstack/localstack`
- `ContainerImageTag`: Matches package version (e.g., `4.10.0`)

### Common Scenarios

#### Artifactory

```csharp
container.ContainerRegistry = "artifactory.company.com";
container.ContainerImage = "docker-local/localstack/localstack";
container.ContainerImageTag = "4.10.0";
// Pulls: artifactory.company.com/docker-local/localstack/localstack:4.10.0
```

#### Azure Container Registry (ACR)

```csharp
container.ContainerRegistry = "mycompany.azurecr.io";
container.ContainerImage = "localstack/localstack";
container.ContainerImageTag = "4.10.0";
// Pulls: mycompany.azurecr.io/localstack/localstack:4.10.0
```

#### AWS Elastic Container Registry (ECR)

```csharp
container.ContainerRegistry = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
container.ContainerImage = "localstack/localstack";
container.ContainerImageTag = "4.10.0";
// Pulls: 123456789012.dkr.ecr.us-west-2.amazonaws.com/localstack/localstack:4.10.0
```

#### GitHub Container Registry (GHCR)

```csharp
container.ContainerRegistry = "ghcr.io";
container.ContainerImage = "myorg/localstack";
container.ContainerImageTag = "custom-build-123";
// Pulls: ghcr.io/myorg/localstack:custom-build-123
```

#### Pin to Specific Version

```csharp
// Only override the tag to use a different LocalStack version
container.ContainerImageTag = "3.8.1";
// Pulls: docker.io/localstack/localstack:3.8.1
```

### Authentication

Container registry authentication is handled by Docker/Podman on the host machine. Ensure you're logged in before running:

```bash
# Docker Hub
docker login

# Private registry
docker login artifactory.company.com

# Azure Container Registry
az acr login --name mycompany

# AWS ECR
aws ecr get-login-password --region us-west-2 | docker login --username AWS --password-stdin 123456789012.dkr.ecr.us-west-2.amazonaws.com
```

### Backward Compatibility

All three properties are optional and default to the public Docker Hub image:

```csharp
// These are equivalent:
builder.AddLocalStack();

builder.AddLocalStack(configureContainer: container =>
{
    container.ContainerRegistry = "docker.io";
    container.ContainerImage = "localstack/localstack";
    container.ContainerImageTag = "4.10.0"; // Package version
});
```

## Logging and Debugging

### Debug Level

Controls LocalStack's [`DEBUG`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) flag for increased log verbosity.

```csharp
container.DebugLevel = 1; // Enable verbose logging
```

**Values:**

- `0` (default): Standard log level
- `1`: Increased log level with more verbose logs (useful for troubleshooting)

### Log Level

Controls LocalStack's [`LS_LOG`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) environment variable.

```csharp
container.LogLevel = LocalStackLogLevel.Debug;
```

**Available Levels:**

- `Trace`: Detailed request/response logging
- `TraceInternal`: Internal calls logging
- `Debug`: Debug level logging
- `Info`: Info level logging
- `Warn`: Warning level logging
- `Error` (default): Error level logging
- `Warning`: Alias for `Warn`

**Note:** `LS_LOG` currently overrides the `DEBUG` configuration in LocalStack.

For more details on LocalStack logging, see the [official logging documentation](https://docs.localstack.cloud/aws/capabilities/config/logging/).

## Docker Socket Access

LocalStack Lambda requires access to Docker on the host to create containers for function execution.

### Enabling Docker Socket

```csharp
container.EnableDockerSocket = true;
```

This mounts `/var/run/docker.sock` from the host into the LocalStack container, allowing it to manage Docker containers.

### When to use

- Running Lambda functions with LocalStack Lambda emulator
- Using container-based LocalStack features

### Security considerations

- Grants the LocalStack container access to Docker on the host
- Only enable when specifically needed for Lambda or other container-based features
- Default is `false` for security best practices

**Note:** The [playground Lambda examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda) use the AWS Lambda emulator (hybrid approach) and do not require this setting. Enable this only if you specifically need LocalStack's Lambda emulator.

## Advanced Environment Variables

For advanced scenarios, you can pass custom environment variables to the LocalStack container:

```csharp
container.AdditionalEnvironmentVariables["LOCALSTACK_API_KEY"] = "your-pro-key";
container.AdditionalEnvironmentVariables["PERSISTENCE"] = "1";
```

**Important:** Do not manually set `SERVICES` or `EAGER_SERVICE_LOADING` - these are managed automatically when using `EagerLoadedServices`. An exception will be thrown if conflicts are detected.

**Common Use Cases:**

- LocalStack Pro features ([`LOCALSTACK_API_KEY`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#localstack-pro))
- [Persistence configuration](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/) (`PERSISTENCE`)
- Custom [configuration options](https://docs.localstack.cloud/aws/capabilities/config/configuration/)

## Configuration Patterns

### Development (Fast Iteration)

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Default Session lifetime is fine for most development
    container.LogLevel = LocalStackLogLevel.Warn;
    // Use lazy loading for faster startup
});
```

### Development (Container Reuse)

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Use Persistent to reuse container between runs
    container.Lifetime = ContainerLifetime.Persistent;
    container.LogLevel = LocalStackLogLevel.Warn;
});
```

### CI/CD

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Default Session lifetime is perfect for CI/CD
    container.LogLevel = LocalStackLogLevel.Error;

    // Eagerly load all services used in tests
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB, AwsService.S3];
});
```

### Enterprise with Private Registry

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Pull from private Artifactory
    container.ContainerRegistry = "artifactory.company.com";
    container.ContainerImage = "docker-local/localstack/localstack";
    container.ContainerImageTag = "4.10.0";

    container.Lifetime = ContainerLifetime.Persistent;
    container.LogLevel = LocalStackLogLevel.Warn;

    // Use static port for consistency with other tools
    container.Port = 4566;
});
```

### Debugging

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Default Session lifetime - clean state for each debug session
    container.LogLevel = LocalStackLogLevel.Debug;
    container.DebugLevel = 1;

    // Use static port for easier debugging
    container.Port = 4566;

    // Eagerly load to see startup issues
    container.EagerLoadedServices = [AwsService.Sqs];
});
```

### Integration Testing

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    // Default Session lifetime - perfect for isolated test runs
    container.LogLevel = LocalStackLogLevel.Error;

    // Dynamic ports by default - allows parallel test runs without conflicts

    // Eagerly load services to avoid cold-start variance
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB];
});
```

## Troubleshooting

### Environment Variable Conflicts

If you get an error about environment variable conflicts:

```text
InvalidOperationException: Cannot set 'SERVICES' or 'EAGER_SERVICE_LOADING'
in AdditionalEnvironmentVariables when using EagerLoadedServices.
```

**Solution:** Remove manual `SERVICES` or `EAGER_SERVICE_LOADING` from `AdditionalEnvironmentVariables`:

```csharp
// ❌ DON'T DO THIS
container.AdditionalEnvironmentVariables["SERVICES"] = "sqs,dynamodb";
container.EagerLoadedServices = [AwsService.S3]; // Conflict!

// ✅ DO THIS
container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB, AwsService.S3];
```

### Container Health Checks Fail

If health checks fail after container starts:

1. **Enable verbose logging:**

   ```csharp
   container.DebugLevel = 1;
   container.LogLevel = LocalStackLogLevel.Debug;
   ```

2. **Check Aspire Dashboard:** Look at health check details for specific failing services

3. **Try eager loading:** Some services may benefit from eager loading:

   ```csharp
   container.EagerLoadedServices = [AwsService.Sqs];
   ```

### Data Not Persisting

If you expect data to persist between runs:

1. **Check container lifetime:** Ensure `ContainerLifetime.Persistent` is set
2. **Enable persistence:** See [LocalStack Persistence documentation](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/)

   ```csharp
   container.AdditionalEnvironmentVariables["PERSISTENCE"] = "1";
   ```

## Best Practices

1. **Use Session lifetime in CI/CD** - Ensures clean state for each test run
2. **Eagerly load services in CI** - Eliminates cold-start variability in automated tests
3. **Keep logging minimal in CI** - Use `LogLevel.Error` for cleaner output
4. **Use Persistent lifetime for development** - Faster iteration with container reuse
5. **Start with lazy loading** - Only use eager loading when you have a specific need
6. **Limit eager loaded services** - Only pre-load services you actually use
7. **Document your configuration** - Add comments explaining configuration choices

## Related Documentation

- [README](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/blob/master/README.md) - Getting started guide
- [LocalStack Configuration](https://docs.localstack.cloud/aws/capabilities/config/configuration/) - Official LocalStack configuration reference
- [LocalStack Persistence](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/) - Data persistence options
- [LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) - Client-side configuration
