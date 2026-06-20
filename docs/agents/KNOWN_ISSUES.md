# Agent Known Notes

Date: 2026-06-18

These notes are hints for agents during triage and review. They are not permission to refactor unrelated code.

- `.github/PULL_REQUEST_TEMPLATE.md` may mention older Aspire/.NET wording.
- `docs/CONFIGURATION.md` can drift from code defaults such as the LocalStack image version.
- Some AWS integration logic may depend on version-sensitive type-name string matching.
- Lambda integration tests contain fixed-delay waits for async SQS/event-source behavior.
- `Aspire.Hosting.LocalStack.csproj` temporarily has direct `AWSSDK.Core` and `MessagePack` package references to avoid NuGet vulnerability restore failures. Remove these pins when the real upstream dependency chain is fixed; this NuGet package should not permanently expose extra direct dependencies, especially `MessagePack`.
