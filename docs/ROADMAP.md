# Roadmap

Date: 2026-07-14

## How To Use This Document

This is a **phased, prioritized research backlog** — not a ready-to-execute plan. Each workstream below still needs its own deep-dive (research + design) before implementation. Priority indicates ordering and importance, not separate releases.

Progress is tracked **in this document** (the Status column below) — there is no separate progress file. Each workstream's detailed plan lives in `docs/plans/wsN-*.md` and is linked from the table once its deep-dive is done.

## Status & Plan Mapping

Status: 🔜 Not started · 🔬 Researching · 📐 Planned · 🔨 In progress · ✅ Done

| WS | Title | Priority | Status | Plan doc |
|----|-------|----------|--------|----------|
| WS0 | Analyzer & .editorconfig modernization | P0 | ✅ | — |
| WS1 | Full package update (foundation) | P0 | ✅ | — |
| WS1.5 | CDK routing evidence pass | P0 | ✅ | — |
| WS2 | Aspire/AWS modernization & feature adaptation | P1 | ✅ | — implemented + runtime-verified 2026-07-04 with integration coverage on the pinned token-free `4.12.0` image; WS7 tracks the future auth-token image decision |
| WS3A | AppHost API boundary + endpoint-conflict warning | P1 | 📐 | [plan](plans/2026-07-14-ws3a-apphost-api-boundary.md) |
| WS3B | Resource-wiring correctness | P1 | 🔜 | — research required before planning |
| WS4 | Bugs & correctness | P2 | 🔜 | — |
| WS5 | Test integrity | P2 | 🔜 | — |
| WS6 | Refactoring / API quality | P2 | 🔜 | — |
| WS7 | LocalStack platform tracking | P2 | 🔜 | — |
| WS8 | Observability / UX features | P3 | 🔜 | — |
| WS9 | Docs & internal-docs consolidation | P0 | ✅ | — |
| WS10 | Aspire ecosystem integration (CLI/catalog/polyglot) | P1 | 📐 | [research](plans/ws10-aspire-ecosystem-integration-research.md) |

## Release Philosophy

Everything here ships in **one release**, governed by:

- Maximize **backward compatibility**. Existing AppHost and consumer code should keep working.
- Where a surface must change, **add the new path alongside the old** and mark the old one `[Obsolete]` (slated for removal in a *following* release) rather than breaking it now.
- **Minimize breaking changes.** Prefer additive, opt-in evolution.

## Original Vision (verbatim)

> Öncelikle amacımız hemen execute edebilecek bir plan oluşturmaktan ziyade TODO/phased prio list yani bunların hepsine ayrı ayrı deep dive edilmesi gerekiyor. Ama tabi research de yapacağız. Şimdi kafamda birkaç şey var;
>
> - Bi kere full latest package update'e gideceğiz
> - Şuan açık olan issue'lara bakacağız
> - Bayadır update çıkmıyoruz, Aspire aldı gitti, yeni feature'lar eklendi, biz onlarla uyumlu muyuz veya gerekli olanlarının adaptosyonlarını yaptık mı, yani bu anlamda projeyi modernize etme ve refactor etmemiz lazım
> - AWS official aspire pluging'ine baya feature eklendi, aslında bizim amacımız bunların LocalStack ortamında çalışabilmesini sağlamak, bu yeni özellikleri de araştırmalıyız, neleri alıp adapte edebiliriz, hangi yeni feature'lar çıkmış
> - İki tane potential feature store'u consolide edip internal-docs'u öldürmemiz lazım. Özellikle TODO.md'de aldığım bir sürü not var bunların anlaşılmayanlarının üzerinden geçmek lazım
> - Known_issue'a eklediğimiz item'ları da roadmap'e almak lazım
> - Genel possible bug, refactoring oppurtunity, testler sağlam mı değil mi? Bugüne kadar yapılan feature'larda bug var mı daha iyi yapılabilir miydi?
> - Bir de zaman içinde fikrim değişti, sanki LocalStack.Client projesinde depend olmamamız gerekiyor, tabiki ürettiği env variable'ları LocalStack.Client kullanan proje uyumlu olmalı ama oradan aldığımız modeller bizi zora sokuyor sanki bunu da araştıralım.
> - Ayrıca Localstack tarafını da takip etmemiz yeni container'a geçmemiz, bizim kullanabileceğimiz feature'lar varsa onlara da bakmak lazım. LocalStack ekibi de baya çalıştı aslında.

---

## Workstreams

### WS1 — Full package update (foundation) · P0

Bring all dependencies to current. Gate for WS2.

- Aspire `13.1.0` → latest `13.4.x`; **`Aspire.Hosting.AWS` `9.3.0` → `13.x`** (major realign — the big one); `LocalStack.Client`, AWSSDK family.
- Decide strategy for the manually-maintained `<Version>` in `Aspire.Hosting.LocalStack.csproj`.
- Refresh the verified `external/` upstream checkouts to the new target refs before WS2.

**Deep-dive:** enumerate breaking changes across the `Aspire.Hosting.AWS` 9.3→13.x jump.

### WS2 — Aspire/AWS modernization & feature adaptation · P1 (depends on WS1)

Mission: make new `Aspire.Hosting.AWS` capabilities work under LocalStack.

**Research:** re-validated 2026-07-02 against `external/` source and web sources. Current conclusion: Aspire core is compatible; the main actionable AWS feature gap is LocalStack support for Lambda DynamoDB Streams event sources (viable on LocalStack, with evidence-backed design constraints folded into this roadmap). AgentCore and AWS publish/deploy are deferred.

**Design/plan:** chosen path: minimal adapter that mirrors the existing SQS helper-resource wiring, plus a stronger `playground/lambda` scenario: async QR generation driven by DynamoDB Streams, a `GET /{slug}/qr` status route, and a control-room web frontend showing the CDC and SQS event paths side by side. Integration coverage now runs against the pinned token-free `4.12.0` image; WS7 tracks the future auth-token image decision.

- **Validate string-typename matching** against the new AWS-integration source. The host matches AWS internals by full type-name string (`Constants.SQSEventSourceResource`, `Constants.CloudFormationReferenceAnnotation`) — version-sensitive and most at risk in the 9.3→13.x jump.
- **Catalog new AWS-integration features** since 9.3.0 and decide which to support on LocalStack: HTTPS Lambda/API Gateway emulators, publish/deploy support, SQS event-source dedupe fix, `AddAWSDynamoDBLocal` return-type change, AgentCore (experimental).
- Audit new Aspire resource-model/interfaces worth adopting.

### WS3A — AppHost API boundary + endpoint-conflict warning · P1

**Scope: the hosting package owns its recommended AppHost configuration contract. `LocalStack.Client` remains an internal runtime dependency, and workload compatibility with `LocalStack.Client.Extensions` is preserved.**

**Plan:** [WS3A AppHost API Boundary Implementation Plan](plans/2026-07-14-ws3a-apphost-api-boundary.md)

- Add package-owned `LocalStackHostingOptions`, canonical `Aspire:Hosting:LocalStack` configuration, validation, and source/binary-compatible `AddLocalStack` overloads.
- Keep the released Client-owned options surface in 13.x as non-error `[Obsolete]` compatibility adapters; remove it only in the next major version.
- Keep emitting `LocalStack__*` environment variables unchanged for workloads using `LocalStack.Client.Extensions`.
- Keep Client models behind an internal immutable hosting-state boundary and translate to `SessionStandalone` only at the runtime seam.
- Emit a read-only best-effort warning when this package can observe Client proxy configuration and native `AWS_ENDPOINT_URL*` configuration on the same workload.
- Record the proxy/native-endpoint signing limitation in `README.md` Known Limitations, `docs/agents/KNOWN_ISSUES.md`, and the draft `13.4.1` changelog. Do not add a native endpoint feature or mutate workload configuration.

**Superseded original direction:** the deep-dive rejected complete removal of the `LocalStack.Client` package, direct construction of the CloudFormation client, and automatic native endpoint emission. `SessionStandalone` preserves proxy-mode behavior, LocalStack.Client owns the canonical service metadata, and the custom-`ServiceURL` spike exposed version-sensitive signing-region behavior. Automatic endpoint emission would also risk overriding explicit user or upstream emulator configuration.

**DynamoDB Local validation:** `AddAWSDynamoDBLocal` remains rejected fail-fast with `UseLocalStack()`. They are competing DynamoDB backends and combining them splits state; revisiting that rule requires a deliberate mixed-backend design rather than incidental endpoint emission.

**Closed scope decisions:** issue #12's required bare-SDK scenario already works through `localStack.Resource.ConnectionStringExpression`, so WS3A does not add `IResourceWithEndpoints`. DynamoDB Local coexistence is already handled by the fail-fast rule above. Native endpoint precedence/signing remains a documented known issue, not a package-managed endpoint feature.

### WS3B — Resource-wiring correctness · P1

**Status: research required before design or implementation planning. No solution has been selected.**

- **Target-aware helper attachment.** `UseLocalStack()` recognizes internal AWS SQS/DynamoDB Streams helper executables by full type-name and attaches LocalStack without knowing which backend the helper targets. Research whether backing-resource intent is recoverable from Aspire/AWS annotations and relationships, whether an explicit opt-out is needed, and whether preserving current behavior is safer.
- **App-like resource coverage.** `ProjectResource` and `ExecutableResource` subclasses are conditionally auto-wired today. `ContainerResource` from `AddContainer`/`AddDockerfile` satisfies the environment/wait capabilities but falls through the concrete-type switch. Research the intended support boundary, capability-based wiring feasibility, callback ordering, and compatibility impact before choosing support or explicit non-support.
- Research both items together because they share the `UseLocalStack()` auto-wiring seam. Return evidence, options, risks, and a recommendation for approval before creating an implementation plan.

### WS4 — Bugs & correctness · P2

- **#24** — `LOCALSTACK_HOST` conflates host port with internal port. In `LocalStackConnectionStringAvailableCallback.cs:40` it is set to the host-facing `{host}:{port}`; when a custom host `Port` is pinned, internal consumers (awslocal, health checks) must still target the internal port `4566`. Tied to todo #7. WS10 research (2026-07-04): the modern fix is network-context-aware endpoint resolution — 13.x `NetworkIdentifier`/`KnownNetworkIdentifiers` + the container tunnel (default since 13.3) give per-network endpoint answers (`LocalhostNetwork` vs `DefaultAspireContainerNetwork`); docker.sock-spawned Lambda containers stay outside Aspire's model and need explicit gateway addressing. See the WS10 research doc.

### WS5 — Test integrity · P2

- **Complete `LocalStackConnectionStringAvailableCallbackTests`** — partially covered (one real CDK-branch test exists, and env injection is covered in `LocalStackResourceConfiguratorTests`); still untested through the callback: SQS/project branch dispatch and the `LOCALSTACK_HOST` assignment. todo #8.
- **De-flake integration tests** — replace fixed `Task.Delay(10s)` waits in the Lambda functional tests with polling / `WaitForResourceHealthyAsync` or AWS-state polling.
- **Known startup flake (Linux CI): "Collection was modified" from `LocalStackLambdaFixture`** — Aspire 13.4.6 annotation-collection race during DCP startup; root-caused 2026-07-09. Upstream fix (microsoft/aspire#18259, thread-safe `ResourceAnnotationCollection`) is merged to main but in no shipped release. Action: re-run the job on occurrence; resolve by bumping Aspire when the first release containing the fix ships (refresh `external/aspire` and verify `ResourceAnnotationCollection.cs` at the new tag). Full evidence and revisit triggers: [investigation](plans/aspire-annotation-race-investigation.md).
- **Slow tests in a separate collection** — todo #13.
- WS10 research (2026-07-04): document the canonical consumer-test pattern (`WaitForResourceHealthyAsync("localstack")` + AWS SDK asserts, no hardcoded 4566 — testing builder randomizes proxied ports) and verify persistent-lifetime containers behave under 13.2 `--isolated` parallel AppHosts (conflict risk — recommend session lifetime in tests).
- **CDK bootstrap & error-path coverage**; review unit tests (todo #11), enrich integration tests (todo #12).
- **Guard tests for reflected AWS types** — todo #14; ties to WS2's string-typename fragility. (Note: a clean `typeof()` replacement may be impossible because those AWS types are `internal` — confirm in WS2.)
- **App-like resource auto-wiring coverage** — add unit coverage for `AddCSharpApp`, JavaScript/Node/Vite, Python, and container/Dockerfile resources that reference CloudFormation/CDK resources. Expected behavior should be explicit: Project/Executable-derived app resources are conditionally auto-wired today; `ContainerResource` is currently a documented gap unless WS3B changes the contract.
- Decide on the failing SQS-event-source emulator tests: real LocalStack limitation → documented skip, or fixable.

### WS6 — Refactoring / API quality · P2

- `LocalStackContainerOptions` immutability (todo #4) — tension with the `configureContainer` mutation pattern; design call needed.
- `UseLocalStack` mutates `builder.Resources` via Remove/Insert to order CDK bootstrap — works but fragile. WS10 research (2026-07-04): the modern replacement is the eventing model — fluent `On*` callbacks, `IDistributedApplicationEventingSubscriber`, and the 13.3 BeforeStart pipeline phase (`SubscribeBeforeStart`) for deterministic rewiring order; `OnResourceStopped` could also clean up docker.sock-spawned Lambda containers. Evaluate `WithHttpHealthCheck`/`WithHttpProbe` vs the bespoke health check (keep per-service fidelity).
- **Move env-annotation registration out of `ConnectionStringAvailableEvent` to model-construction time** — deferred `WithEnvironment(context => ...)` callbacks resolving LocalStack endpoint values lazily; the event callback keeps only non-annotation side effects (CloudFormation client setup, CDK credential override, asset-upload customizer). Motivated by the 2026-07-09 annotation-race flake ([investigation](plans/aspire-annotation-race-investigation.md)) but valid independently: event-time model mutation is fragile — annotations added after a target's env has been computed are silently ignored. **Design constraint:** today's "this package's env callbacks always run last" property exists *because* registration happens at event time. Moving to build time changes callback precedence and requires an explicit policy; WS3A does not introduce automatic native endpoint emission or a general defaults-not-overrides rule.
- Post-WS2 helper-resource cleanup — SQS and DynamoDB Streams support share the same string-matched internal AWS helper-resource pattern across `UseLocalStack()`, `LocalStackConnectionStringAvailableCallback`, constants, and configurators. Consider a small internal abstraction instead of adding more hardcoded branches. The two dispatch chains must be edited in lockstep for every new helper type; the abstraction should carry, per helper, the type-name constant, LocalStack.Client environment emission, and whether reference/wait attachment applies.
- Helper-resource dashboard UX — evaluate `WithHidden()` / `WithHiddenOnCompletion()` for implementation-detail helpers such as SQS/DynamoDB Streams pollers and CDK bootstrap resources.
- Suppress the noisy client-side `"Failed to connect to AWS using AWS SDK config..."` warning (todo #15).
- Fix the misleading eager-service error message in `LocalStackResourceBuilderExtensions.cs` — it reports a service "is not supported by LocalStack" when the real cause is a missing CLI-name mapping in `LocalStack.Client` (from the PR #8 review).

### WS7 — LocalStack platform tracking · P2

- **New unified single image** migration; native `LOCALSTACK_AUTH_TOKEN` support (issue #25). Urgency raised 2026-07-02: since 2026-03-23 new LocalStack releases ship as a single image and require an auth token (including in CI); the pinned default `4.12.0` stays token-free but frozen. Workaround remains `AdditionalEnvironmentVariables`. Couples with WS2's integration-test image choice — see the LocalStack Platform Constraints section of the WS2 research doc.
- Decide the DynamoDB Streams integration-test image strategy after WS2 unit coverage lands: keep the pinned token-free `4.12.0` image for now, or move integration coverage to the unified image with `LOCALSTACK_AUTH_TOKEN` and CI secret plumbing.
- **Pro features** — research which we can support natively (todo #6).
- **Lambda debugging support** (todo #5).
- WS10 research (2026-07-04) mechanisms for this workstream: model `LOCALSTACK_AUTH_TOKEN` as a secret `ParameterResource` with dashboard/InteractionService prompting persisted to user secrets (InteractionService is still `[Experimental]` in 13.4 — gate accordingly); document `aspire secret set` flow; optional named volume for `/var/lib/localstack` persistence; expose `WithImagePullPolicy` (incl. `Never` for air-gapped CI) on container options.

### WS8 — Observability / UX features · P3

- Surface individual AWS/LocalStack resources (CloudFormation stacks, Lambdas, S3 buckets, SQS queues, DynamoDB tables) on the Aspire dashboard (todo #1) and/or a standalone resource-viewer UI (todo #16) — these overlap; consolidate. Inspired by the AWS .NET team's Lambda Test Tool UI.
- **#26** — provide an escape hatch to the underlying container (bind mounts / init scripts) and/or a resource-ready provisioning hook (e.g. SES `VerifyEmailIdentity`). Relates to Pro (WS7). WS10 research (2026-07-04) mechanisms: `WithContainerFiles()` ships init scripts into `/etc/localstack/init/ready.d` without bind mounts (kills Windows/CI friction), and fluent `OnResourceReady` is the provisioning-hook surface.
- Dashboard/CLI/MCP presentation pass (from WS10 research): `WithCommand`/`WithHttpCommand` ("reset state", "dump diagnostics") with `ResourceCommandVisibility` + typed args, `WithUrls` (health endpoint, LocalStack Web App), `WithIconName`, parent/child nesting of auto-wired AWS resources under the LocalStack resource, `ExcludeFromMcp()` on noisy helpers. Overlaps todo #1/#16 — same consolidation.
- Optional: a dedicated `playground/eager-loading/` example AppHost — eager loading is currently exercised only in integration tests, not shown as a runnable sample (from the PR #8 review).

### WS10 — Aspire ecosystem integration (CLI/catalog/polyglot) · P1

Mission: the full "what changed in the Aspire world, what new support can this package add" sweep — TypeScript/polyglot AppHost compatibility and `aspire add`/catalog discoverability, plus a capability-adoption pass over the modern hosting toolbox (dashboard commands/URLs/icons, `WithContainerFiles` init hooks, volumes, secret parameters + interaction service for the auth token, eventing hooks, MCP visibility). Several adoption candidates land inside existing workstreams (WS4 #24, WS7 #25, WS8 #26) — WS10 researches and routes them rather than duplicating them.

**Research:** see [`docs/plans/ws10-aspire-ecosystem-integration-research.md`](plans/ws10-aspire-ecosystem-integration-research.md) (2026-07-04). Source-verified headlines:

- Third-party ATS/TypeScript export is officially supported and attribute-based (`[AspireExport]` family + `Aspire.Hosting.Integration.Analyzers`); custom `WithReference` semantics need `IResourceWithCustomWithReference<LocalStackResource>` (Qdrant is the reference implementation) or TS AppHosts silently get default connection-string wiring instead of this package's LocalStack wiring.
- The integration catalog (`aspire add` / `aspire integration list` / MCP `list_integrations`) is gated by a **hardcoded package-ID prefix** (`Aspire.Hosting.*` — Microsoft-reserved on NuGet — or `CommunityToolkit.Aspire.Hosting.*`). `LocalStack.Aspire.Hosting` is invisible and cannot be force-added even by exact ID. TS AppHosts can still consume the package by hand-editing `aspire.config.json` (arbitrary IDs are officially supported there) — just not via `aspire add`. Strategy decision needed: CommunityToolkit re-homing vs upstream filter-widening issue vs status quo with documented manual flows.

Research is complete as of 2026-07-04 (two adversarially cross-checked passes: ecosystem/community process + Aspire 9.0→13.4 feature-evolution sweep; ranked top-10 adoption candidates in the research doc). Adoption mechanisms are routed into WS3A/WS3B/WS4/WS5/WS6/WS7/WS8 bullets; WS10 retains the ATS/polyglot export work, the TS validation AppHost, and the catalog strategy decision (CommunityToolkit re-homing vs upstream filter-widening issue vs documented manual flows — Deniz's call).

Implementation status rechecked 2026-07-08: WS10 is not implemented and is not obsolete. The package still lacks ATS export/analyzer setup and the `polyglot` tag, `LocalStackResource` still lacks `IResourceWithCustomWithReference<LocalStackResource>`, no TypeScript validation AppHost exists, and the catalog strategy decision is still open.

Sequencing: WS2 has landed; coordinate the ATS-export surface with WS3A's API shape; helper-hiding/dashboard UX stays in WS6/WS8.

### WS9 — Docs & internal-docs consolidation · P0 (light, do early)

- ✅ Consolidated `internal-docs/` into this roadmap and deleted the folder (gitignored/historical; all live items captured in the triage table and workstreams). Raw notes now go to the Inbox section below instead of `internal-docs/todo.md`.
- Remaining: fix doc drift surfaced in WS4 and `KNOWN_ISSUES.md`.

## todo.md Triage

Status: ✅ understood · ⚠️ partial · ❓ unclear

| # | Item | One-line meaning | Status | Workstream |
|---|------|------------------|--------|------------|
| 1 | AWS resources on Aspire dashboard | Surface CF stacks/lambdas/buckets/queues/tables as dashboard child-resources. | ✅ | WS8 |
| 2 | Redirect `awsdynamodblocal` to LocalStack | Rejected: DynamoDB Local and LocalStack's DynamoDB are competing backends; `UseLocalStack()` fails fast rather than splitting state. | ✅ | WS3A decision |
| 3 | Make `CreateServiceConfig<T>()` public + reflection | LocalStack.Client `Session`/`SessionReflection` remains an internal runtime seam; no public hosting API requires this method. **Parked.** | ❓ | WS6 (park) |
| 4 | `LocalStackContainerOptions` immutable? | Make it init-only/record — conflicts with `configureContainer` mutation; design call. | ✅ | WS6 |
| 5 | LocalStack debugging support | Lambda debugging (attach debugger to Lambda in LocalStack). | ✅ | WS7 |
| 6 | LocalStack Pro features | Research which Pro features we can support natively. | ⚠️ | WS7 |
| 7 | Option host/port should not be used | Container endpoint comes from Aspire; `Config.LocalStackHost/EdgePort` must not be source of truth. Same root as #24. | ✅ | WS3A/WS4 |
| 8 | Complete `...CallbackTests` | Stub confirmed; test the real callback behavior. | ✅ | WS5 |
| 9 | `.WithHttpEndpoint(port: options.Config.EdgePort...)` | Wire host port from client config EdgePort — **superseded by #7 and WS3A endpoint ownership. Recommend drop.** | ⚠️ | (drop) |
| 10 | Remove options from `AddLocalStack` | Drop `ILocalStackOptions?` param (mark `[Obsolete]`). | ✅ | WS3A |
| 11 | Review unit tests | General review pass. | ✅ | WS5 |
| 12 | Enrich integration tests | Broaden coverage. | ✅ | WS5 |
| 13 | Slow integration tests in own collection | TUnit grouping for slow tests. | ✅ | WS5 |
| 14 | Tests + wrappers for reflected AWS resources | Guard tests + abstraction over reflected AWS-int types. | ✅ | WS5/WS2 |
| 15 | Prevent "Failed to connect to AWS..." warning | Suppress noisy AWS SDK default-profile warning client-side. | ✅ | WS6 |
| 16 | Mini UI for LocalStack resources | Standalone resource viewer (overlaps #1); inspired by AWS Lambda Test Tool UI. | ✅ | WS8 |

## KNOWN_ISSUES.md Mapping

| Note | Workstream |
|------|------------|
| PR template mentions older Aspire/.NET wording | WS9 |
| `docs/CONFIGURATION.md` drifts from image-version default | WS4/WS9 |
| Version-sensitive type-name string matching | WS2 |
| Fixed-delay waits in Lambda integration tests | WS5 |
| DynamoDB Streams event sources require `us-east-1` (upstream SDK signing regression in the Lambda Test Tool custom-endpoint path) | Upstream watch — lift playground pin + README/CHANGELOG known-issue when fixed |
| CI-only "Collection was modified" startup flake (Aspire 13.4.6 annotation race) | WS5 + upstream watch — bump Aspire when a release contains microsoft/aspire#18259; refactor option tracked in WS6 |

## Open GitHub Issues Mapping

| Issue | Summary | Disposition |
|-------|---------|-------------|
| #12 | Bare AWS SDK endpoint composition works through `ConnectionStringExpression`; no endpoint-interface change is planned | WS3A decision |
| #24 | `LOCALSTACK_HOST` port mismatch with custom port | WS4 |
| #25 | Single-image `LOCALSTACK_AUTH_TOKEN` requirement | WS7 (urgency raised — transition live since 2026-03-23; workaround exists) |
| #26 | SES v1 needs Pro; no public container exposure | WS8 (escape hatch) + WS7 (Pro) |
| #18 | LocalStack for Azure | **Out of scope** (future, large) |

## Out Of Scope (for this release)

- **#18 LocalStack for Azure** — large, separate effort.

## Open Questions

- **#9** defaulted to "drop" (superseded by #7/WS3A) — confirm.
- **WS6 / `LocalStackContainerOptions` immutability** — resolve the design tension with the mutation-based `configureContainer` callback.

## Inbox / Untriaged

Drop raw, unsorted ideas here as they come up, then triage them into a workstream (and remove them from this list) during roadmap grooming. This replaces the retired `internal-docs/todo.md` capture spot.

- Discoverability: `aspire integration list` / Aspire MCP `list_integrations` catalogs official + CommunityToolkit packages only — `LocalStack.Aspire.Hosting` is not listed (checked 2026-07-04). Root cause found same day (hardcoded ID-prefix filter) — folded into WS10; the remaining open item is Deniz's catalog strategy decision.
- Hybrid real-AWS mode idea (WS10 sweep): model real AWS endpoints as `AddExternalService()` resources so playgrounds can flip LocalStack ↔ real AWS per resource.
- Watch LocalStack platform for an MCP endpoint in the container image; if it ships, annotate with `WithMcpServer(path)` (13.2) so agents auto-discover it.
- Optional deliberate opt-in: `PublishAsDockerComposeService()` passthrough for teams that want LocalStack in generated compose for CI (docker.sock + privileged); default stays `ExcludeFromManifest`.
- Upstream issue candidates from WS2 runtime verification (2026-07-04, evidence in the WS2 design doc): (a) aws-lambda-dotnet — Lambda Test Tool's bundled AWSSDK.Core 4.0.7.x loses the signing region when `AWS_ENDPOINT_URL*` is set (empirical matrix captured; breaks non-us-east-1 LocalStack); (b) aws integrations — API Gateway emulator route config is one-per-Lambda-resource, second `WithReference` silently overwrites the first.
- Consumer guidance to document (WS3A/WS9): LocalStack.Client's default proxy-mode registration leaks the AWS regional host into generated URL strings (e.g. presigned URLs). Either build browser-facing URLs with an S3UrlService-style LocalStack-aware helper (the playground's approach) or register the client with `AddAwsService<T>(useServiceUrl: true)` when genuine presigning is required.
- CodeQL C# analysis quality (2026-07-09): GitHub default setup currently scans C# with `build-mode: none`, producing a low analysis-quality warning (`call target` coverage 81%, threshold 85%). Evaluate advanced CodeQL setup with manual .NET restore/build so generated code, dependencies, and call targets are represented more accurately; do not block the current CI hardening work on this.
