# Roadmap

Date: 2026-10-16

## How To Use This Document

This is the prioritized backlog, not an implementation plan. Open work needs research or design where stated. Status refers to work implemented in this checkout; **done does not mean merged or published**. Release history belongs in [CHANGELOG.md](../CHANGELOG.md), runtime decisions in [the hosting model](architecture/hosting-model.md), and upstream defects and closing triggers in [Upstream Watch](upstream-watch.md).

Audit baseline: `d745144`. PR [#36](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/pull/36) was open and unmerged on the date above; WS3A and the October dependency update are part of the unreleased `13.6.0` entry, not assumed to be on `master`. Package versions come from `Directory.Packages.props`, not historical research notes.

## Status

🔜 Not started · 🔬 Research / refresh required · 🔨 In progress · ✅ Implemented / completed

| WS | Title | Priority | Status | Evidence / next step |
| --- | --- | --- | --- | --- |
| WS0 | Analyzer modernization | P0 | ✅ | `80c9566`; policy follow-up is WS0.5 |
| WS0.5 | Analyzer arbitration rebase | P0 | 🔬 | Re-derive policy for this repository; packages already installed |
| WS1 | Package update | P0 | ✅ | `b62ec39`; current pins in `Directory.Packages.props` |
| WS1.5 | CDK routing evidence and fix | P0 | ✅ | `8fd8c99`; released in `13.4.0` |
| WS2 | Aspire/AWS modernization | P1 | ✅ | `9d60d80`; released in `13.4.0` |
| WS3A | AppHost API boundary + endpoint-conflict warning | P1 | ✅ | `7b078e5`; implemented, awaiting merge/release; [architecture](architecture/hosting-model.md) |
| WS3B | Resource-wiring correctness | P1 | 🔜 | Research and scope approval required |
| WS4 | Bugs & correctness | P2 | 🔜 | Issues #24, #34, #35 |
| WS5 | Test integrity | P2 | 🔜 | Remaining gaps and fixed waits below |
| WS6 | Refactoring / API quality | P2 | 🔜 | Design decisions below |
| WS7 | LocalStack platform tracking | P2 | 🔜 | Image/auth-token decision remains open |
| WS8 | Observability / UX features | P3 | 🔜 | Dashboard and provisioning candidates below |
| WS9 | Docs & internal-docs consolidation | P0 | ✅ | Consolidation and this groom complete; ongoing behavior docs travel with their owning workstream |
| WS10 | Aspire ecosystem integration | P1 | 🔬 | [July research](plans/ws10-aspire-ecosystem-integration-research.md) needs refresh; not an approved implementation plan |
| WS11 | Upstream reports | P1 | 🔬 | One draft; second report needs a reproduction |

## Release Direction

- Preserve backward compatibility through additive, opt-in evolution. Retained Client-shaped APIs are non-error `[Obsolete]` adapters until the next major, as recorded in the unreleased changelog.
- The original backlog grouped everything into one release. That is no longer a description of progress: `13.4.0` already shipped part of it, while `13.6.0` is unreleased. The remaining release scope needs confirmation; workstream priority alone is not a release commitment.

## Completed Work

- **WS0:** analyzer configuration modernization landed in `80c9566`. That completed pass is separate from the arbitration rebase below.
- **WS1:** the foundation update landed, followed by the October refresh in `b62ec39`. `1609129` aligned the unreleased package version with Aspire's major.minor. Refresh upstream source checkouts against the pins whenever later compatibility work needs them; this is not an unfinished upgrade task.
- **WS1.5:** CDK asset-upload STS/S3 routing landed in `8fd8c99`, closing #29/#30. The reason for the SDK-hook approach is in [the hosting model](architecture/hosting-model.md#the-cdk-asset-uploader-is-redirected-through-aws-sdk-hooks-not-process-environment).
- **WS2:** DynamoDB Streams helper wiring, the asynchronous QR flow, and playground frontend work are recorded in the `13.4.0` changelog. Test coverage exists against the pinned image. Future image choice belongs to WS7; upstream signing restrictions belong to Upstream Watch. The original AWS 9.3→13.x discovery tasks are historical, not pending implementation instructions. AgentCore and AWS publish/deploy support remain deferred.
- **WS3A:** package-owned hosting options, canonical configuration, compatibility overloads, immutable hosting state, and a read-only endpoint-conflict warning are implemented. The unreleased changelog owns the API inventory; [the hosting model](architecture/hosting-model.md) owns overload constraints and warning-ordering limits. `LocalStack.Client` remains a runtime dependency, workload environment compatibility is preserved, and no general native-endpoint emission feature was added. DynamoDB Local remains a competing backend rejected by `UseLocalStack()`.
- **WS9:** `internal-docs/` was consolidated in `3f04f4f`; the consumer-doc correction (`055696e`) and agent/architecture consolidation (`a443aba`, `c70d16f`) followed. This groom removes the duplicate known-notes file: test risks live in WS5, warning mechanics in the hosting model, and upstream defects in Upstream Watch. There is no separate known-issues mapping to maintain.

## Open Workstreams

### WS0.5 — Analyzer Arbitration Rebase · P0

Research the arbitration model in [`Blind-Striker/opencode-sdk-dotnet`](https://github.com/Blind-Striker/opencode-sdk-dotnet), especially its `docs/engineering/quality-gates.md` and `.editorconfig`, then derive a policy for this repository rather than copying it. Candidate direction: shipped defaults for default-on rules, deliberate overrides with reasons and removal triggers, scoped test exceptions, and compilation-wide configuration where disabling an analyzer rather than only its diagnostics is justified by measurement.

**Baseline correction:** Meziantou, BannedApiAnalyzers, VisualStudio.Threading, and NSubstitute analyzers are already installed through the root/test build props; their versions live in `Directory.Packages.props`. `BannedSymbols.txt` already bans `DateTime.Now` and related local-time APIs. This is a policy review, not a missing-package task. Evaluate whether the AWS SDK's mutually exclusive endpoint/region setters warrant additional bans without preventing legitimate configuration paths.

`c86e4fb` already removed the dead `CA1801` entry and corrected the suppression heading. Enumeration still needs review, but that removed entry is historical evidence, not a current defect. Overrides can also become stale; validate them against diagnostics that actually fire, using known-live controls and distinguishing no measurement from a measured absence.

Keep analyzer policy for this repository's build distinct from public API compiler compatibility: net8.0 consumers can still use a C# 12 compiler, which constrains the overload contract documented in the hosting model. Implementation of this rebase is outside the current documentation groom.

### WS3B — Resource-wiring Correctness · P1

**Research required before scope, design, or implementation planning. No solution selected.**

- **Target-aware helper attachment:** `UseLocalStack()` recognizes internal SQS/DynamoDB Streams helper executables by full type name without checking which backend they target. Research recoverable resource intent, explicit opt-out options, and the compatibility cost of changing current behavior.
- **App-like resource coverage:** Project/Executable-derived resources referencing CloudFormation/CDK are conditionally auto-wired. Containers satisfy environment/wait capabilities but fall through the concrete-type switch. Research the intended support boundary, capability-based wiring, callback ordering, and compatibility impact.
- Investigate both at the shared auto-wiring seam. Return evidence, options, risks, and a recommendation for scope approval before an implementation plan. Scope stress-testing remains user-triggered.

### WS4 — Bugs & Correctness · P2

- **#24 — host/internal-port mismatch:** `LocalStackConnectionStringAvailableCallback` still assigns `LOCALSTACK_HOST` from the host-facing connection string. Research endpoint resolution for host clients, Aspire-managed containers, and docker.sock-spawned Lambda containers separately. Network-aware endpoints/tunnel behavior are candidates from the old WS10 research, not a selected fix.
- **#34 — health-check names:** registrations and resource attachments still use the shared `LocalStackHealthCheckName`. The open issue reports duplicate-name failure with two LocalStack resources; add a reproduction before choosing a per-resource naming fix. No new runtime measurement was made during this groom.
- **#35 — Docker socket configuration:** `DOCKER_HOST` is set unconditionally while the socket mount is gated by `EnableDockerSocket`. The inconsistency is present in source; a runtime failure has not been established. Decide whether to gate the variable or document why it is unconditional.

### WS5 — Test Integrity · P2

- **Callback coverage:** `LocalStackConnectionStringAvailableCallbackTests` already exercises CDK configuration and DynamoDB Streams dispatch. Remaining direct-callback gaps include SQS/project dispatch and asserting the `LOCALSTACK_HOST` value. Configurator tests cover environment emission separately; this is not an empty stub.
- **Fixed waits:** `LocalStackLambdaFunctionalTests` still has three fixed ten-second waits in its SQS analytics tests. Replace these with bounded AWS-state polling; the QR path already uses bounded status polling. Resource health alone does not prove that a particular queued event has completed.
- **Reflected-type guards:** `ConstantsTests` already checks existence and expected interfaces/base types for all three AWS internal types. Keep these guards aligned with dependency updates; do not recreate them. Behavior-level dispatch coverage and any shared helper abstraction remain separate work (WS3B/WS6).
- **App-like auto-wiring tests:** cover C# apps, JavaScript/Node/Vite, Python, and container/Dockerfile resources after WS3B establishes expected behavior. Current container omission is a research gap, not an approved support extension.
- **Broader test audit:** review CDK bootstrap/error paths, remaining unit assertions, integration scenarios, and whether slow tests need grouping beyond the existing integration non-parallel group.
- **Consumer-test guidance:** document allocated endpoints, resource readiness, and SDK assertions; investigate persistent-lifetime behavior under isolated parallel AppHosts. Revalidate version-specific CLI/API suggestions from WS10 before publishing guidance.
- **Historical SQS failure note — unverified:** the old backlog mentioned failing emulator tests without a current reproducer. The tests remain active; this groom did not execute them. Require a failing run before classifying the behavior as a LocalStack limitation or proposing a skip.

The annotation-collection startup race is closed in [Upstream Watch](upstream-watch.md#closed-aspire-annotation-collection-race-on-linux-ci); it is not a pending test fix. The separate callback-registration design item remains in WS6.

### WS6 — Refactoring / API Quality · P2

- **Container options immutability:** decide whether init-only/record options can coexist with the mutation-based `configureContainer` contract.
- **CDK bootstrap ordering:** `UseLocalStack()` still uses resource Remove/Insert. Evaluate eventing-based ordering rather than assuming a particular hook replaces it correctly.
- **Environment callback timing:** consider registering deferred environment callbacks at model construction, keeping only non-annotation side effects in the connection-string event. Preserve or deliberately redesign precedence: the current late registration makes these callbacks run last. See [the hosting model](architecture/hosting-model.md#wiring-is-deferred-to-the-connection-string-callback).
- **Helper dispatch duplication:** evaluate a small internal abstraction for SQS/DynamoDB Streams type names, environment emission, and reference/wait attachment across both dispatch chains. Coordinate with WS3B.
- **Helper visibility:** evaluate hiding implementation-detail resources in coordination with WS8; visibility is distinct from wiring correctness.
- **Health checks:** evaluate standard health/probe APIs only if eager-service fidelity is retained.
- **Client warning noise:** investigate the `"Failed to connect to AWS using AWS SDK config..."` warning before choosing suppression or another fix.
- **Eager-service error wording:** a missing LocalStack.Client CLI-name mapping currently reports that the service is unsupported by LocalStack. Correct the diagnostic when that work is approved.
- **Package identity:** published builds supply `PackageId=LocalStack.Aspire.Hosting` in CI, but the project file does not own that ID. Align local pack and published identity.
- **Parked:** exposing Client session/configuration helpers publicly has no established hosting-API requirement. Revisit only with a concrete consumer case.

### WS7 — LocalStack Platform Tracking · P2

- **#25 — image/auth-token support:** the current pin is runtime-verified token-free; which newer images can start without a token remains an explicit unknown. [Upstream Watch](upstream-watch.md#live-localstack-requires-an-auth-token-on-current-images) owns the platform evidence and next measurement. Choose the integration-test image strategy from that evidence, not from the claim that the current pin is necessarily the last token-free image.
- Evaluate native token configuration and CI secret plumbing; the existing escape hatch is `AdditionalEnvironmentVariables`. Secret parameters and Interaction Service are adoption candidates. The old claim that the entire Interaction Service is experimental is obsolete at pinned Aspire `v13.6.1`; gate individual APIs as needed.
- Research licensed features and Lambda debugging support.
- Revisit persistence volumes, image-pull policy, and interactive service selection after the WS10 refresh. These are candidates, not approved additions.

### WS8 — Observability / UX Features · P3

- Consolidate dashboard child-resources and the standalone resource-viewer idea (old todo #1/#16): CloudFormation stacks, Lambdas, buckets, queues, and tables. Choose a surface before building both.
- **#26 — provisioning/container escape hatch:** research init scripts and resource-ready provisioning for cases such as SES email-identity verification. Separate the package hook from LocalStack licensing requirements (WS7).
- Evaluate dashboard commands, URLs, icons, parent/child relationships, and deliberate MCP visibility after WS10 refreshes the API evidence. Coordinate helper visibility with WS6.
- Consider a runnable eager-loading playground; current coverage lives in integration tests.

### WS9 — Documentation Maintenance Ownership · P0

The consolidation is complete (see Completed Work). Remaining feature-specific docs travel with their owning workstream: topology with WS4, consumer-test guidance with WS5, platform/token guidance with WS7, and ecosystem guidance with WS10. New documentation defects can reopen WS9, but resolved template wording and matching image defaults are not outstanding bugs.

### WS10 — Aspire Ecosystem Integration · P1

**Refresh required, not implementation-ready.** The [research document](plans/ws10-aspire-ecosystem-integration-research.md) records the July baseline. The October dependency update moved beyond that baseline; this groom does not re-run the ecosystem research or endorse all of its source/API claims.

- Refresh CLI/catalog discoverability, ATS/polyglot authoring, analyzer setup, and the broader hosting-toolbox sweep against the actual pins. Version-sensitive mechanisms in that document are leads, not current facts.
- The package source still has no ATS export/analyzer opt-in, no custom `IResourceWithCustomWithReference` implementation, and no TypeScript validation AppHost. Determine the current required mechanism before implementing one.
- Catalog strategy remains a decision: CommunityToolkit re-homing, an upstream filter-widening proposal, or documented manual consumption. Reverify the catalog restriction before choosing between them.
- Route adoption candidates to WS3B/WS4/WS5/WS6/WS7/WS8 rather than duplicating implementation tasks. WS3A's API shape is implemented; merge/release status is tracked above, not an unresolved API-design prerequisite.

### WS11 — Upstream Reports · P1

[Upstream Watch](upstream-watch.md) owns the signing evidence, tool versions, and closing triggers; keep measurements there rather than reproducing them in this backlog.

- **`aws/aws-lambda-dotnet`:** a Lambda Test Tool bundled-SDK report draft exists from the previous session. Its table explanation needs the same first-two-rows correction made in Upstream Watch before sending. Filing remains with Deniz; after filing, add the issue URL to the matching trigger. This groom did not edit the draft, repeat the duplicate search, or file a report.
- **`aws/integrations-on-dotnet-aspire-for-aws`:** the July runtime session reported API Gateway emulator route configuration being overwritten by a second Lambda reference. Its cited WS2 design document is absent from the current tree. Re-establish a minimal reproduction against the pinned integration before filing; this is not a newly confirmed upstream defect.

## Historical todo.md Dispositions

These are old **note IDs**, not GitHub issue numbers or test-completion checkboxes. Workstream sections above own the actionable details.

| Note ID | Topic | Disposition |
| --- | --- | --- |
| 1 / 16 | Dashboard resources / standalone viewer | Consolidated in WS8 |
| 2 | Redirect DynamoDB Local to LocalStack | Rejected; existing fail-fast contract |
| 3 | Public `CreateServiceConfig<T>()` / reflection | Parked in WS6 |
| 4 | Container options immutability | WS6 design decision |
| 5 / 6 | Lambda debugging / Pro support | WS7 |
| 7 | Client host/port as source of truth | AppHost endpoint ownership implemented; remaining topology defect is WS4 #24 |
| 8 | Callback tests | Partial coverage; remaining gaps in WS5 |
| 9 | Host port from Client `Config.EdgePort` | Superseded by endpoint ownership; recommendation to drop awaits confirmation |
| 10 | Remove Client options from `AddLocalStack` | WS3A added owned overloads and deprecated the retained compatibility path |
| 11 / 12 / 13 | Unit review / integration enrichment / slow grouping | WS5 |
| 14 | Reflected AWS-type guards / wrappers | Guards exist; dispatch coverage WS5, abstraction WS6 |
| 15 | Client-side AWS config warning | WS6 |

## Open GitHub Issues

GitHub state checked on the document date. The workstream owns the disposition; an implementation decision here does not close an upstream issue automatically.

| Issue | Topic | Disposition |
| --- | --- | --- |
| #12 | Endpoint interface for bare SDK consumers | Existing connection-string expression covers the recorded scenario; no interface addition selected |
| #24 | `LOCALSTACK_HOST` port mismatch | WS4 |
| #25 | New-image auth token | WS7 |
| #26 | SES / container access / provisioning | WS8 + WS7 licensing |
| #34 | Shared health-check name | WS4 |
| #35 | Unconditional `DOCKER_HOST` | WS4 |
| #18 | LocalStack for Azure | Out of scope for this release; separate effort |

## Open Decisions

- Confirm the remaining release scope now that part of the original one-release backlog has shipped.
- Confirm dropping old todo note #9; this is not GitHub issue #9.
- Resolve container-options immutability (WS6), the image/token strategy (WS7), and catalog ownership strategy after refreshed evidence (WS10).

## Inbox / Untriaged

Capture new ideas here, then move them to an owning workstream during grooming. Already-triaged catalog discoverability belongs only to WS10.

- Hybrid real-AWS mode: investigate per-resource LocalStack ↔ AWS switching; `AddExternalService()` was an earlier candidate, not a selected design.
- Watch for a LocalStack-container MCP endpoint; evaluate discovery annotations only if one becomes available.
- Optional opt-in Compose publishing for CI while keeping the default dev-only manifest exclusion; feasibility and docker.sock/security implications need research.
- Consumer URL guidance: distinguish proxy-routed API requests from browser-facing/presigned URL generation. The playground uses a LocalStack-aware helper; validate any alternative registration advice before documenting it.
- CodeQL analysis quality: a July run reported low call-target coverage under default C# `build-mode: none`. Re-check the warning before proposing advanced setup; no current quality measurement was made during this groom.
