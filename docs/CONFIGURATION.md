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
| `ContainerImageTag` | `string?` | `null` (selected runtime preset) | Custom container image tag/version |
| `EnableDockerSocket` | `bool` | `false` | Mount Docker socket for Lambda support |
| `DebugLevel` | `int` | `0` | LocalStack DEBUG flag (0 or 1) |
| `LogLevel` | `LocalStackLogLevel` | `Error` | LocalStack LS_LOG level |
| `AdditionalEnvironmentVariables` | `IDictionary<string, string>` | `{}` (empty) | Custom environment variables |

## Host Configuration

Host-level LocalStack settings control whether the LocalStack resource is created and how it authenticates. These use the canonical `Aspire:Hosting:LocalStack` configuration section.

> **LocalStack is disabled by default.** `LocalStackHostingOptions.Enabled` defaults to `false`, so until you opt in `AddLocalStack` returns `null`, `UseLocalStack(null)` is a no-op, and no container starts. Every `configureContainer` example in this guide assumes that opt-in is already in place. Opt in one of three ways: set `Aspire:Hosting:LocalStack:Enabled` to `true` in configuration (shown below), set the legacy `LocalStack:UseLocalStack` to `true`, or pass a `configureOptions` callback that calls `.WithEnabled(true)`.

### Canonical Configuration

```json
{
  "Aspire": {
    "Hosting": {
      "LocalStack": {
        "Enabled": true,
        "Region": "us-east-1",
        "AccessKeyId": "accessKey",
        "SecretAccessKey": "secretKey",
        "SessionToken": "token"
      }
    }
  }
}
```

Environment variable equivalents:

```text
Aspire__Hosting__LocalStack__Enabled=true
Aspire__Hosting__LocalStack__Region=us-east-1
Aspire__Hosting__LocalStack__AccessKeyId=accessKey
Aspire__Hosting__LocalStack__SecretAccessKey=secretKey
Aspire__Hosting__LocalStack__SessionToken=token
```

> **Migration note:** The legacy `LocalStack:*` section (e.g. `"LocalStack": { "UseLocalStack": true }`) remains supported for backward compatibility. Canonical `Aspire:Hosting:LocalStack:*` settings take precedence when both are present. New projects should use the canonical section.

### C# Callback Configuration

Hosting options can also be set programmatically in the `AddLocalStack` callback:

```csharp
var localStack = builder.AddLocalStack(
    "localstack",
    awsConfig,
    options => options
        .WithEnabled(true)
        .WithRegion("us-east-1")
        .WithCredentials("accessKey", "secretKey", "token"),
    container => container.Lifetime = ContainerLifetime.Session);
```

Options are resolved from lowest to highest precedence: built-in defaults, the legacy `LocalStack:*` section, the canonical `Aspire:Hosting:LocalStack:*` section, the `configureOptions` callback, and finally `IAWSSDKConfig.Region`. The callback therefore overrides configuration file values, but an `awsConfig` that carries a region overrides the callback's region.

## Runtime Selection and Container Activation

These runtime/onboarding APIs are part of the unreleased baseline work. Final
shared runtime acceptance remains required before release.

The current preset selects `docker.io/localstack/localstack:2026.10.6`.
`container.WithLegacy()` selects the explicitly supported token-free compatibility
candidate `4.14.0`; `WithLegacy(false)` restores the current preference. Legacy
describes a runtime choice, not an integration edition or the obsolete
Client-shaped API. It does not promise every new capability or vendor backports.

```csharp
var localStack = builder.AddLocalStack(
    "localstack", awsConfig: null,
    options => options.WithEnabled(true),
    container => container.WithLegacy());
```

Explicit non-null `ContainerRegistry`, `ContainerImage` and `ContainerImageTag`
values override the selected preset independently, regardless of assignment order.
A different mirror, image or floating tag is custom, not a verified legacy/current
identity. There is no runtime registry lookup, automatic downgrade or real-AWS
fallback. An explicit override that exactly matches the qualified current image
still has the current image's activation requirement.

### Activation Is Not AWS Identity

Current LocalStack images require a LocalStack activation token and appropriate
account entitlement. Obtain/manage it in the
[LocalStack Web App](https://app.localstack.cloud/workspace/auth-tokens).
The package does not log in, manage accounts/licenses, install a CLI, or turn
that token into AWS access keys, session credentials or Client workload settings.

Provide `LOCALSTACK_AUTH_TOKEN` through the AppHost's secure configuration or
environment, or pass an explicit source in the container callback:

```csharp
var activation = builder.AddParameter("localstack-activation", secret: true);
var localStack = builder.AddLocalStack(
    "localstack", awsConfig: null,
    options => options.WithEnabled(true),
    container => container.WithAuthToken(activation));
```

`WithAuthToken(string)` also accepts a value already obtained from secure
configuration. Do not hardcode tokens or commit them to configuration files.
The parameter overload requires `Secret == true` and retains the original
resource without reading its value.

Package-owned source precedence is:

1. Last explicit `WithAuthToken` call, including a blank string.
2. Exact `AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"]` entry.
3. `builder.Configuration["LOCALSTACK_AUTH_TOKEN"]`, normally supplied by the
   standard host environment/configuration providers.
4. No binding.

An explicit empty/whitespace value suppresses lower-priority fallback. A failed
explicit parameter never switches to an ambient token. Winning nonblank scalar
inputs become private secret parameters with no published default; caller-owned
secret parameters stay deferred. The package creates no automatic activation
parameter/binding/guard when disabled or publishing, and does not remove
caller-owned parameters used elsewhere. LocalStack itself remains excluded from
the deployment manifest.

The exact known-current image gets a safe missing-binding error before any container
is created: the resource reaches `FailedToStart`, and its log names the resource and
`LOCALSTACK_AUTH_TOKEN`. `StartAsync` does not throw for it.
Deferred bindings are left to Core's resolution/cancellation behavior;
LocalStack validates actual token/license/entitlement validity. Legacy/custom
images do not acquire an inferred token requirement, and supplied values are
not stripped from them.

Configure image/binding changes during ordinary model construction. Later generic
Core environment calls retain their normal precedence, but deliberately injecting
a raw string through `WithEnvironment` does not retroactively gain secret
protection. Prefer a secret parameter. Hooks appended after this integration's
finalization are outside the guard's ordering guarantee.

Secret flags protect normal framework presentation, not host/container
administrators, arbitrary object serialization or vendor logs. Scalar selection
is snapshotted while building the AppHost; it is not hot credential rotation.
Persistent-container reuse/recreation and rotation still need runtime acceptance;
the package does not delete user data or license caches to make them work.

### Shared Runtime Validation and Trusted CI

The C# entry point runs the full integration project for either `current` or
`legacy`, using standard `dotnet test`. This includes the common runtime scenarios
and the configuration/manifest tests; it does not filter the project to the common
manifest. Native HTML, JSON and TRX reports remain enabled. Error messages, stack
traces and live command output are retained, with activation tokens masked rather
than entire diagnostic sections removed.

```sh
dotnet tools/localstack-tool.cs -- run-runtime legacy artifacts/runtime/legacy
dotnet tools/localstack-tool.cs -- run-runtime current artifacts/runtime/current
dotnet tools/localstack-tool.cs -- validate-runtime artifacts/runtime/current artifacts/runtime/legacy
```

Use new or empty output directories. Current requires `LOCALSTACK_AUTH_TOKEN` in
the process environment, never in command arguments. Credential-bearing reports
are generated in private scratch space and masked before being copied to the
artifact directory. An immediate authenticated HTML uploader cannot bypass that
boundary: the command fails before starting rather than disabling reporting or
uploading an unmasked report. The existing secret-free/unit upload path is retained.

Each run writes `profile-run.json` (`schema_version` 2). It records the execution-time
revision and source cleanliness, an `execution_id`, and the SHA-256 of `runtime.trx`
and of every host image receipt. The integration tests receive the same id through
`LOCALSTACK_TEST_EXECUTION_ID` and write it into each receipt. Receipts show model
selection, not successful startup by themselves. The validator requires:
- both complete passing profiles;
- a matching clean revision and configuration;
- the same executed test identities;
- every fixture/direct-host receipt carrying its run's id;
- an artifact inventory whose hashes match the files exactly.
Evidence from an older schema, a dirty development run or a missing credential
cannot certify a release. The hashes prove consistency within one run's folder, not
authenticity against someone who can rewrite the whole folder.

Report publication fails closed when an activation token is present. Every report
is masked, then checked for the raw, HTML-encoded and URI-escaped token in its
relative path, its text, its JSON keys and the decoded TUnit HTML payload, before
anything is copied. An unsupported file type, a malformed known format or an
unknown HTML payload layout fails the run. Masked reports still reach the output
when a later validation step fails. A run without a token copies its reports
unchanged. The child writes the GitHub step summary to private scratch; the runner
masks and checks it on every exit path before appending it to the real summary.

PR validation uses only secret-free legacy checks. The trusted pair runs in the
reusable workflow `.github/workflows/runtime-pair.yml`, in the `localstack-current`
GitHub environment, which holds `LOCALSTACK_CI_AUTH_TOKEN` as an environment secret
and requires a reviewer's approval. It never uses a personal-token fallback or an
unreviewed PR checkout. It runs on canonical master pushes and on a manual CI run
that names the exact selected SHA. Every publication path needs the passing pair
for the same SHA, plus a `verify-consumer` pass on the packed archive:
- the CI workflow's continuous deployment;
- the manual `publish-nuget.yml` workflow, whose trust approval is the environment's
  reviewer approval.
A feature-package publication additionally needs the manual run's publication flag.
The environment, its secret and its protection rules are repository settings the
owner maintains.

The independent SDK8/C#12 packed-consumer check is:

```sh
dotnet tools/localstack-tool.cs -- verify-consumer artifacts/runtime-baseline-pack artifacts/compatibility/consumer
```

It needs one locally packed `LocalStack.Aspire.Hosting` archive and an already
installed SDK8. It checks the archive's actual SHA512, builds against published
13.4.0 and the candidate separately, then executes the old binary with the
candidate's verified assembly/dependency graph. It neither installs an SDK nor
starts services. Restore caches and staged binaries are temporary; logs and hash
evidence remain in the output directory. A compatibility pass does not replace
runtime acceptance.

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
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3];
});
```

This sets the [`EAGER_SERVICE_LOADING=1`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) and [`SERVICES`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#core) environment variables automatically.

> **Enabled, preloaded and health-required sets differ.** The package emits `SERVICES` and requests eager loading, but the selected backend applies its available-provider filtering, mandatory dependencies and strict-loading policy. Do not assume the list is an exact final allow-list across images. Package health still requires every requested service to report `running`; relaxing backend restrictions does not preload or health-check everything.

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
    AwsService.DynamoDb,      // DynamoDB
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
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    container.ContainerRegistry = "artifactory.company.com";  // Where to pull from
    container.ContainerImage = "docker-mirrors/localstack/localstack";  // Image path
    container.ContainerImageTag = "2026.10.6";  // Pin to a specific LocalStack version
    container.WithAuthToken(activation);  // A mirror is a custom identity; the image still needs its token
});
```

**Defaults:**

- `ContainerRegistry`: `docker.io` (Docker Hub)
- `ContainerImage`: `localstack/localstack`
- `ContainerImageTag`: `2026.10.6`, the qualified current image: a fixed pin, not a floating `latest`. It requires a LocalStack activation token (see [Runtime Selection and Container Activation](#runtime-selection-and-container-activation)). `WithLegacy()` selects the token-free `4.14.0`, the last release before LocalStack moved from semver to calendar versions.

An image whose registry, path or tag differs from the qualified current image is a custom identity. The package does not check activation for custom images, but a current LocalStack image still needs `LOCALSTACK_AUTH_TOKEN`. Pass it with `WithAuthToken`.

### Common Scenarios

#### Artifactory

```csharp
container.ContainerRegistry = "artifactory.company.com";
container.ContainerImage = "docker-local/localstack/localstack";
container.ContainerImageTag = "2026.10.6";
// Pulls: artifactory.company.com/docker-local/localstack/localstack:2026.10.6
```

#### Azure Container Registry (ACR)

```csharp
container.ContainerRegistry = "mycompany.azurecr.io";
container.ContainerImage = "localstack/localstack";
container.ContainerImageTag = "2026.10.6";
// Pulls: mycompany.azurecr.io/localstack/localstack:2026.10.6
```

#### AWS Elastic Container Registry (ECR)

```csharp
container.ContainerRegistry = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
container.ContainerImage = "localstack/localstack";
container.ContainerImageTag = "2026.10.6";
// Pulls: 123456789012.dkr.ecr.us-west-2.amazonaws.com/localstack/localstack:2026.10.6
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
container.ContainerImageTag = "2026.10.0";
container.WithAuthToken(activation);
// Pulls: docker.io/localstack/localstack:2026.10.0
```

Any tag other than the qualified current one is a custom identity. The package does not check activation for it, so check the image's own requirements. Every LocalStack image from `2026.03.0` on, `latest` included, requires `LOCALSTACK_AUTH_TOKEN`; pass it with `WithAuthToken`.

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

builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    container.ContainerRegistry = "docker.io";
    container.ContainerImage = "localstack/localstack";
    container.ContainerImageTag = "2026.10.6"; // The qualified current image, spelled out
});
```

An explicit override that exactly matches the qualified current image keeps the current image's activation requirement, so both forms need the token.

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
container.AdditionalEnvironmentVariables["LOCALSTACK_AUTH_TOKEN"] = "your-auth-token";
container.AdditionalEnvironmentVariables["PERSISTENCE"] = "1";
```

**Important:** Do not manually set `SERVICES` or `EAGER_SERVICE_LOADING` - these are managed automatically when using `EagerLoadedServices`. An exception will be thrown if conflicts are detected.

**Common Use Cases:**

- Licensed LocalStack features and newer LocalStack images, both of which need [`LOCALSTACK_AUTH_TOKEN`](https://docs.localstack.cloud/aws/capabilities/config/configuration/#localstack-for-aws) - on LocalStack 4.x the auth token replaced `LOCALSTACK_API_KEY`, which no longer activates a license
- [Persistence configuration](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/) (`PERSISTENCE`)
- Custom [configuration options](https://docs.localstack.cloud/aws/capabilities/config/configuration/)

## Configuration Patterns

### Development (Fast Iteration)

```csharp
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    // Default Session lifetime is fine for most development
    container.LogLevel = LocalStackLogLevel.Warn;
    // Use lazy loading for faster startup
});
```

### Development (Container Reuse)

```csharp
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    // Use Persistent to reuse container between runs
    container.Lifetime = ContainerLifetime.Persistent;
    container.LogLevel = LocalStackLogLevel.Warn;
});
```

### CI/CD

```csharp
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    // Default Session lifetime is perfect for CI/CD
    container.LogLevel = LocalStackLogLevel.Error;

    // Eagerly load all services used in tests
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3];
});
```

### Enterprise with Private Registry

```csharp
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    // Pull from private Artifactory
    container.ContainerRegistry = "artifactory.company.com";
    container.ContainerImage = "docker-local/localstack/localstack";
    container.ContainerImageTag = "2026.10.6";
    container.WithAuthToken(activation);

    container.Lifetime = ContainerLifetime.Persistent;
    container.LogLevel = LocalStackLogLevel.Warn;

    // Use static port for consistency with other tools
    container.Port = 4566;
});
```

### Debugging

```csharp
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
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
builder.AddLocalStack("localstack", awsConfig: null, configureContainer: container =>
{
    // Default Session lifetime - perfect for isolated test runs
    container.LogLevel = LocalStackLogLevel.Error;

    // Dynamic ports by default - allows parallel test runs without conflicts

    // Eagerly load services to avoid cold-start variance
    container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb];
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
container.EagerLoadedServices = [AwsService.Sqs, AwsService.DynamoDb, AwsService.S3];
```

### Container Health Checks Fail

If health checks fail after container starts:

1. **Enable verbose logging:**

   ```csharp
   container.DebugLevel = 1;
   container.LogLevel = LocalStackLogLevel.Debug;
   ```

2. **Check Aspire Dashboard:** Look at health check details for specific failing services

3. **Review `EagerLoadedServices`:** The check requires every requested service to report `running`. Backend-enabled services can also include dependencies; this is not an exact cross-image allow-list. Correct the required list to match the workload rather than hiding a service failure merely to obtain a green check:

   ```csharp
   // With no services listed the check only requires the container to report healthy
   container.EagerLoadedServices = [];
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
6. **Choose eager services deliberately** - Requested services add preload work and health requirements; backend dependencies and strict-loading policy govern the final enabled set
7. **Document your configuration** - Add comments explaining configuration choices

## Related Documentation

- [README](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/blob/master/README.md) - Getting started guide
- [LocalStack Configuration](https://docs.localstack.cloud/aws/capabilities/config/configuration/) - Official LocalStack configuration reference
- [LocalStack Persistence](https://docs.localstack.cloud/aws/capabilities/state-management/persistence/) - Data persistence options
- [LocalStack.NET Client](https://github.com/localstack-dotnet/localstack-dotnet-client) - Client-side configuration
