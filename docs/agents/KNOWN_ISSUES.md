# Agent Known Notes

Date: 2026-06-18

These notes are hints for agents during triage and review. They are not permission to refactor unrelated code.

- `.github/PULL_REQUEST_TEMPLATE.md` may mention older Aspire/.NET wording.
- `docs/CONFIGURATION.md` can drift from code defaults such as the LocalStack image version.
- Some AWS integration logic may depend on version-sensitive type-name string matching.
- Lambda integration tests contain fixed-delay waits for async SQS/event-source behavior.
- `Aspire.Hosting.LocalStack.csproj` temporarily has direct `AWSSDK.Core` and `MessagePack` package references to avoid NuGet vulnerability restore failures. Remove these pins when the real upstream dependency chain is fixed; this NuGet package should not permanently expose extra direct dependencies, especially `MessagePack`.
- DynamoDB Streams event sources only work in `us-east-1`: the Lambda Test Tool's bundled AWS SDK signs custom-endpoint requests for `us-east-1` (live upstream regression; evidence and version timeline in `docs/plans/aws-sdk-signing-region-investigation.md`). Symptom: the stream poller loops on `ResourceNotFoundException` for a table that exists. Do not "fix" this in package code — the env emission is correct; lift the playground's `us-east-1` pin and the README/CHANGELOG known-issue entries when upstream ships a fix.
