# LocalStack Configuration Guide

This guide covers configuration options for customizing LocalStack container behavior in .NET Aspire applications.

## Quick Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EagerLoadedServices` | `IReadOnlyCollection<AwsService>` | `[]` (empty) | AWS services to pre-load at container startup |
| `Lifetime` | `ContainerLifetime` | `Persistent` | Container lifecycle behavior |
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

### Persistent (Default)

Container survives application restarts. Data may persist between debugging sessions depending on your configuration.

```csharp
container.Lifetime = ContainerLifetime.Persistent;
```

**When to use:**

- Local development (container reuse between runs)
- When combined with [LocalStack persistence](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/)

### Session

Container is created when the application starts and destroyed when it stops.

```csharp
container.Lifetime = ContainerLifetime.Session;
```

**When to use:**

- CI/CD pipelines (clean slate for each run)
- Integration tests (isolated test runs)
- When you want guaranteed clean state

**Recommendation:** Use `Session` for CI/CD and integration tests, `Persistent` for local development.

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

### Development

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Persistent;
    container.LogLevel = LocalStackLogLevel.Warn;
    // Use lazy loading for faster startup
});
```

### CI/CD

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Session;
    container.LogLevel = LocalStackLogLevel.Error;

    // Eagerly load all services used in tests
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDB, AwsService.S3];
});
```

### Debugging

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Session;
    container.LogLevel = LocalStackLogLevel.Debug;
    container.DebugLevel = 1;

    // Eagerly load to see startup issues
    container.EagerLoadedServices = [AwsService.Sqs];
});
```

### Integration Testing

```csharp
builder.AddLocalStack(configureContainer: container =>
{
    container.Lifetime = ContainerLifetime.Session;
    container.LogLevel = LocalStackLogLevel.Error;

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
