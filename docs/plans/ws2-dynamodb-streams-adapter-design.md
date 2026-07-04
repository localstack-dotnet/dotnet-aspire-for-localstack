# WS2 DynamoDB Streams Adapter Design

Date: 2026-07-04

## Decision

Implement WS2 as a minimal LocalStack adapter for AWS Aspire's DynamoDB Streams event-source helper, plus a stronger playground scenario in the existing Lambda sample: async QR generation driven by DynamoDB Streams, a `GET /{slug}/qr` status route, and a small control-room web frontend that makes both event paths observable.

The package change stays intentionally small: recognize `Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource`, attach it to LocalStack through the existing `WithReference(localstack)` path, and inject the AWS SDK endpoint/credential environment expected by the external Lambda Test Tool process.

The playground change demonstrates a genuinely stream-shaped workflow: URL creation writes to DynamoDB synchronously, then DynamoDB Streams triggers async QR generation.

## Source Model

AWS Aspire uses two different local-development mechanisms that share the same AppHost graph:

- CDK and CloudFormation resources model real AWS infrastructure. `AddAWSCDKStack`, `AddDynamoDBTable`, and `AddSQSQueue` all synthesize CloudFormation through a parent stack. This package already routes that provisioning path to LocalStack by replacing/configuring the CloudFormation client for `ICloudFormationTemplateResource`/`IStackResource` resources.
- Lambda, API Gateway, SQS event source, DynamoDB Local, and DynamoDB Streams event source are local helper/emulator resources. They are local-only and commonly use `ExcludeFromManifest()` and run-mode/publish-mode checks upstream.

`WithDynamoDBStreamsEventSource(...)` creates a helper executable that runs `dotnet lambda-test-tool start --dynamodbstreams-eventsource-config env:DYNAMODB_STREAMS_EVENTSOURCE_CONFIG`. That helper owns its AWS SDK clients, so this package cannot replace those clients the way it can replace the CloudFormation client. Endpoint routing must be done with AWS SDK environment variables.

## Package Design

Add support by mirroring the existing SQS event-source path.

- Add a new internal full-type-name constant for `Aspire.Hosting.AWS.Lambda.DynamoDBStreamsEventSourceResource`.
- Extend `UseLocalStack()` detection so DynamoDB Streams helper executables receive `.WithReference(localstack)` and therefore wait on LocalStack.
- Extend `LocalStackConnectionStringAvailableCallback` with a DynamoDB Streams branch before the CloudFormation relationship fallback.
- Add a configurator method for DynamoDB Streams helper resources.
- Emit `AWS_ENDPOINT_URL`, `AWS_ENDPOINT_URL_DYNAMODB`, `AWS_ENDPOINT_URL_DYNAMODB_STREAMS`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`, `AWS_REGION`, and `AWS_DEFAULT_REGION`.

The service-specific endpoint variables are required, not cosmetic. AWS SDK endpoint precedence lets `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS` override the global endpoint, and upstream AWS Aspire emits those same service-specific variables for its own DynamoDB Local path. The .NET SDK resolves region from `AWS_REGION`; `AWS_DEFAULT_REGION` is kept for CLI-convention compatibility, since the AWS SDK for .NET does not read it.

Endpoint emission must not clobber pre-existing service-specific endpoints. Env callbacks execute in annotation order and this package's callback always runs last (added at connection-string-available time, after upstream's build-time callback), so an unconditional write would override upstream's DynamoDB Local wiring in mixed scenarios and silently point the poller at a backend where the table does not exist. The configurator therefore writes `AWS_ENDPOINT_URL_DYNAMODB` and `AWS_ENDPOINT_URL_DYNAMODB_STREAMS` only when the keys are absent from the environment dictionary: upstream's DynamoDB Local values (or an explicit user `WithEnvironment` override) win, while the pure-LocalStack path — where nothing pre-sets those keys — is unaffected. The global `AWS_ENDPOINT_URL`, credentials, and region are always written; service-specific precedence keeps them harmless in mixed scenarios.

Known limitation, accepted for WS2: in the mixed scenario the helper still receives `WithReference(localstack)` and thus a spurious `WaitFor(localstack)`, because `UseLocalStack()` matches helpers by type name without knowing their backing store. Fixing attachment targeting is a dispatcher redesign — recorded as a WS3 design input, out of WS2 scope.

Known upstream limitation, found during runtime verification (2026-07-04): Amazon.Lambda.TestTool (verified at 0.14.1/0.15.0 — the latest published — bundling AWSSDK.Core 4.0.7.x) loses the client's signing region whenever any `AWS_ENDPOINT_URL*` environment variable applies a ServiceURL — the region nulls out and requests sign for us-east-1 regardless of `AWS_REGION`, `AWS_DEFAULT_REGION`, or even an explicitly set `RegionEndpoint` (verified empirically across all combinations; newer AWSSDK.Core 4.0.9.x preserves the region). DynamoDB Local is not region-scoped so upstream never sees this; LocalStack scopes both tables and streams per region, so for non-us-east-1 regions the streams poller fails: `DescribeTable` returns `ResourceNotFoundException`, and even with `DYNAMODB_SHARE_DB=1` (which shares only the table database, not the Kinesis-backed streams) the follow-up `DescribeStream` fails because the ARN is minted for the signing region. **The only working configuration today is us-east-1**, which the playground pins with an explanatory comment. This package's env emission is correct per the AWS SDK contract; when the tool ships a fixed SDK, other regions work without any package change and the playground can move back to a non-default region. Follow-up: file an upstream issue on aws-lambda-dotnet with the empirical matrix.

Two further runtime findings (2026-07-04), both playground-scoped:

- The API Gateway emulator stores **one route per Lambda resource** — the route-config environment variable is keyed by resource name, so a second `WithReference(lambda, method, path)` silently overwrites the first. The playground therefore registers `QrStatusLambda` as a second Lambda resource backed by the same Redirector project (the handler already dispatches on route). Candidate upstream issue.
- LocalStack.Client's default client registration routes requests through **proxy settings** while the request URI keeps the AWS regional host. Data-plane calls work, but **presigned URLs leak the AWS host** and are dead links for browsers. The Redirector registers its S3 client with `AddAwsService<IAmazonS3>(useServiceUrl: true)` and pins the presign `Protocol` to the client scheme. Worth documenting as consumer guidance (WS3/WS9): any consumer generating presigned URLs against LocalStack needs ServiceURL mode.

## Test Strategy

Unit tests are the WS2 gate.

- Extend `ConstantsTests` so reflection guards the new upstream internal type.
- Add configurator coverage that verifies the DynamoDB Streams environment callback is attached and emits the global plus service-specific endpoints.
- Add or extend resource-scanning/callback tests so the helper receives LocalStack configuration through the same route as SQS.
- Pin endpoint precedence both ways: when `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS` are already present (upstream DynamoDB Local wiring or a user override), the configurator must not overwrite them; when absent, it must set them to LocalStack.

Integration tests are deferred until the LocalStack image/auth-token decision is resolved. LocalStack DynamoDB Streams is viable, but stream configuration does not persist across restarts and newer LocalStack images require auth-token plumbing. This is tracked under WS7.

## Playground Design

Extend `playground/lambda` instead of creating a new playground.

Current sample shape is already ideal: a CDK-backed URL shortener stack creates DynamoDB/S3/SQS resources in LocalStack, while Lambda and API Gateway are local AWS Aspire emulators. WS2 adds a second event-source style without duplicating the SQS analytics flow, plus a read path and a frontend that make both event paths visible.

### Stream flow

1. `UrlShortenerLambda` writes the URL item to `UrlsTable` with QR metadata such as `QrStatus = Pending`.
2. `UrlsTable` enables DynamoDB Streams with a new-image stream view.
3. A new `QrCodeGeneratorLambda` subscribes with `WithDynamoDBStreamsEventSource(...)`.
4. The stream processor handles `INSERT` events, generates a QR PNG, writes it to `QrBucket`, and updates the same URL item with `QrStatus = Ready`, `QrObjectKey`, and timestamp/error metadata.
5. The stream processor ignores non-`INSERT` events so its metadata update does not recursively trigger QR generation.
6. The sample surfaces eventual consistency explicitly: URL creation can return before QR generation completes.

### QR status route

The Redirector Lambda gains a second API Gateway route, `GET /{slug}/qr`:

- Unknown slug: `404`.
- `QrStatus` not yet `Ready`: `202` with a small JSON status body.
- `QrStatus = Ready`: `302` to a short-lived presigned S3 URL for the QR PNG.

This makes eventual consistency externally observable through the same public API: creation returns immediately, and the QR route flips from `202` to `302` when the stream processor catches up. A browser `img` element pointed at the route renders the PNG by following the redirect.

### Control-room frontend

A new ASP.NET Core project (`LocalStack.Lambda.Frontend`) serves a single static page with vanilla JavaScript — deliberately no SPA framework and no npm build step.

- Shorten form and link list: new rows appear immediately with a pending QR cell; polling flips the cell to the PNG once the stream path completes.
- Analytics feed: recent `url_created`/`url_accessed` events read from the analytics table, showing the explicit SQS event path next to the CDC path on one screen.
- The frontend calls the public API through the API Gateway emulator endpoint (injected as configuration by the AppHost) and reads DynamoDB through `LocalStack.Client.Extensions`.

The frontend is also the first plain `ProjectResource` consumer in the playground: it exercises `UseLocalStack()`'s project-configuration branch (`LocalStack__*` env injection consumed by `LocalStack.Client.Extensions`) — a package feature previously visible only in unit tests.

### Path separation

- SQS remains an explicit application event path.
- DynamoDB Streams demonstrates change-data-capture from table writes without the producer publishing a second event.

Telemetry and logging should make the async QR path visible, but package implementation must not depend on playground telemetry behavior. DynamoDB stream records do not propagate trace context, so the stream processor's traces start as new roots — document this in the sample instead of working around it.

## Out Of Scope For WS2

- Do not redesign the `UseLocalStack()` dispatcher or introduce a general helper-resource abstraction in this workstream.
- Do not migrate LocalStack image/auth-token behavior in this workstream.
- Do not adapt AWS Aspire publish/deploy-to-real-AWS behavior for LocalStack.
- Do not add a new playground unless the existing Lambda playground becomes too crowded during implementation.

## Follow-Up Debt

Record these outside WS2 so they are not lost:

- WS6: `UseLocalStack()` and `LocalStackConnectionStringAvailableCallback` now have repeated hardcoded helper-resource branches. After WS2, consider a small internal abstraction for known AWS helper resources shared by SQS and DynamoDB Streams. The abstraction should carry per-helper: type-name constant, env emission, and reference/wait attachment. It should also unify env emission on the don't-clobber guard — the SQS configurator currently overwrites unconditionally (latent only because no SQS local-emulator variant exists).
- WS3: adopt the WS2 endpoint precedence contract package-wide for native `AWS_ENDPOINT_URL_*` emission — LocalStack values are defaults, never overrides. Also design target-aware helper attachment: `UseLocalStack()`'s type-name-only scan attaches LocalStack (and a spurious `WaitFor`) to helpers that may target other backends such as DynamoDB Local. Both insights are detailed in `docs/ROADMAP.md` under WS3.
- WS6: helper resources may become better dashboard citizens with `WithHidden()`/`WithHiddenOnCompletion()`, but that is a UX/refactor decision, not required for functionality.
- WS7: decide whether DynamoDB Streams integration coverage should stay on the pinned token-free `4.12.0` image or move to the unified LocalStack image with `LOCALSTACK_AUTH_TOKEN` support and CI secret plumbing.
