# Agent Known Notes

Date: 2026-07-09

These notes are hints for agents during triage and review. They are not permission to refactor unrelated code.

- `.github/PULL_REQUEST_TEMPLATE.md` may mention older Aspire/.NET wording.
- `docs/CONFIGURATION.md` can drift from code defaults such as the LocalStack image version.
- Some AWS integration logic may depend on version-sensitive type-name string matching.
- Lambda integration tests contain fixed-delay waits for async SQS/event-source behavior.
- DynamoDB Streams event sources only work in `us-east-1`: the Lambda Test Tool's bundled AWS SDK signs custom-endpoint requests for `us-east-1`. Symptom: the stream poller loops on `ResourceNotFoundException` for a table that exists. Do not "fix" this in package code — the env emission is correct; lift the playground's `us-east-1` pin and the README/CHANGELOG known-issue entries when upstream ships a fix.
- LocalStack.Client proxy mode and native `AWS_ENDPOINT_URL*` configuration must not be combined. The SDK exposes the native endpoint as `ServiceURL` while traffic still follows LocalStack.Client's proxy; affected Core versions can then sign a non-default-region request for `us-east-1`. This package does not automatically emit, remove, or overwrite native endpoint values. Bare SDK custom-endpoint clients have the same signing defect. WS3A must not auto-emit native endpoint variables. Detection may warn but must not mutate user values. Evidence: `docs/plans/ws3-api-boundary-decision-scratchpad.md` and [LocalStack.Client #27](https://github.com/localstack-dotnet/localstack-dotnet-client/issues/27#issuecomment-4937111791).
- **The conflict warning is best-effort, read-only, and ordered.** The warning callback is registered during `BeforeStartEvent` and inspects the shared environment dictionary at evaluation time. It does not add, remove, or overwrite environment values. Because Aspire container dependency discovery can evaluate callbacks with `NullLogger`, the detector captures a logger from `ResourceLoggerService` rather than relying on `EnvironmentCallbackContext.Logger`. It cannot observe environment callbacks that are appended by another `BeforeStartEvent` subscriber, a later lifecycle hook, or a pipeline step after this package's callback registration.
- Linux-CI-only integration-test flake: many tests fail at once, each at `0ms`, with `InvalidOperationException: Collection was modified` thrown from `LocalStackLambdaFixture.InitializeAsync`. Known Aspire 13.4.6 startup race — `ResourceAnnotationCollection` is not thread-safe in that release; fixed upstream in microsoft/aspire#18259 (merged to main 2026-06-19, in no shipped release yet). Re-run the job; do not hot-fix package code for this. Evidence and revisit triggers: `docs/plans/aspire-annotation-race-investigation.md`.

## Playwright MCP on Linux (Headless)

**Symptom:** `aspire doctor` or Playwright MCP initialization fails with `Chromium distribution 'chrome' is not found at /opt/google/chrome/chrome`.

**Context:** The `@playwright/mcp` server expects a Chrome binary at the system path `/opt/google/chrome/chrome`. On a headless Linux VM (no UI), the standard `npx playwright install chrome` does not create this path.

**Fix:** Install the Playwright `chromium-headless-shell` build and symlink it:

```bash
npx playwright install chromium --with-deps
sudo mkdir -p /opt/google/chrome
sudo ln -s \
  ~/.cache/ms-playwright/chromium_headless_shell-*/chrome-headless-shell-linux64/chrome-headless-shell \
  /opt/google/chrome/chrome
```

The headless shell is a lightweight Chromium build for automation; it does not require a desktop environment. Restart OpenCode after fixing the path so the MCP server picks it up.
