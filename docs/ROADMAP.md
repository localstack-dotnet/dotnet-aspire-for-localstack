# Roadmap

Date: 2026-06-19

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
| WS2 | Aspire/AWS modernization & feature adaptation | P1 | ✅ | [research](plans/ws2-aspire-aws-modernization-research.md), [design](plans/ws2-dynamodb-streams-adapter-design.md), [implementation plan](plans/ws2-dynamodb-streams-adapter-implementation-plan.md) — implemented + runtime-verified 2026-07-04; integration tests stay gated on WS7 |
| WS3 | AppHost decoupling + native endpoint support | P1 | 🔜 | — |
| WS4 | Bugs & correctness | P2 | 🔜 | — |
| WS5 | Test integrity | P2 | 🔜 | — |
| WS6 | Refactoring / API quality | P2 | 🔜 | — |
| WS7 | LocalStack platform tracking | P2 | 🔜 | — |
| WS8 | Observability / UX features | P3 | 🔜 | — |
| WS9 | Docs & internal-docs consolidation | P0 | ✅ | — |
| WS10 | Aspire ecosystem integration (CLI/catalog/polyglot) | P1 | 🔬 | [research](plans/ws10-aspire-ecosystem-integration-research.md) |

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

**Research:** see [`docs/plans/ws2-aspire-aws-modernization-research.md`](plans/ws2-aspire-aws-modernization-research.md) — re-validated 2026-07-02 against `external/` source and web sources. Current conclusion: Aspire core is compatible; the main actionable AWS feature gap is LocalStack support for Lambda DynamoDB Streams event sources (viable on LocalStack, with evidence-backed design constraints in the research doc). AgentCore and AWS publish/deploy are deferred.

**Design/plan:** see [`docs/plans/ws2-dynamodb-streams-adapter-design.md`](plans/ws2-dynamodb-streams-adapter-design.md) and [`docs/plans/ws2-dynamodb-streams-adapter-implementation-plan.md`](plans/ws2-dynamodb-streams-adapter-implementation-plan.md). Chosen path: minimal adapter that mirrors the existing SQS helper-resource wiring (service-specific endpoint emission guarded so pre-existing values win), plus a stronger `playground/lambda` scenario: async QR generation driven by DynamoDB Streams, a `GET /{slug}/qr` status route, and a control-room web frontend showing the CDC and SQS event paths side by side. Integration coverage waits on the WS7 image/auth-token decision.

- **Validate string-typename matching** against the new AWS-integration source. The host matches AWS internals by full type-name string (`Constants.SQSEventSourceResource`, `Constants.CloudFormationReferenceAnnotation`) — version-sensitive and most at risk in the 9.3→13.x jump.
- **Catalog new AWS-integration features** since 9.3.0 and decide which to support on LocalStack: HTTPS Lambda/API Gateway emulators, publish/deploy support, SQS event-source dedupe fix, `AddAWSDynamoDBLocal` return-type change, AgentCore (experimental).
- Audit new Aspire resource-model/interfaces worth adopting.

### WS3 — AppHost decoupling from LocalStack.Client + native endpoint support · P1

**Scope: the hosting package only. Consumer-side compatibility with LocalStack.Client.Extensions is preserved, not removed.**

- **AppHost-internal (core work):** remove the hosting package's dependency on the `LocalStack.Client` NuGet — replace internal use of `ILocalStackOptions`/`LocalStackOptions`/`ConfigOptions`/`SessionOptions` and the `SessionStandalone`-built CloudFormation client with our own minimal config model and a directly-constructed AWS SDK client (`AmazonCloudFormationConfig { ServiceURL = ... }`). Expected side effect: the temporary direct `MessagePack` / `AWSSDK.Core` pins can be dropped.
- **Consumer-side (preserve + add):**
  - **Keep** emitting `LocalStack__*` environment variables so projects using `LocalStack.Client.Extensions` keep working unchanged. Non-negotiable.
  - **Add** AWS-official `AWS_ENDPOINT_URL_<SERVICE>` (and global `AWS_ENDPOINT_URL`) emission so projects using the bare AWS SDK work without the client library (issue #12). This matches AWS's own idiom (`AddAWSDynamoDBLocal` and our SQS-event-source path already use it).
- Remove the `ILocalStackOptions?` parameter from `AddLocalStack` (mark `[Obsolete]`, keep working) — todo #10.
- Evaluate `IResourceWithEndpoints` (issue #12).

**Validation sub-case (was todo #2):** with native per-service endpoints, "use the official `AddAWSDynamoDBLocal` for DynamoDB while LocalStack serves the rest" largely falls out for free. Not a standalone feature — a consequence of this workstream. Open to challenge.

**Insights from WS2 (2026-07-04, DynamoDB Streams design review):**

- **Endpoint precedence contract.** AWS SDK precedence (`AWS_ENDPOINT_URL_<SERVICE>` > global `AWS_ENDPOINT_URL`) is the mechanism that makes mixed backends viable, and env-callback ordering is last-writer-wins with this package's callbacks always running last (annotated at connection-string-available time, after upstream's build-time callbacks). Unconditional writes therefore silently clobber upstream's own local-emulator wiring: upstream sets `AWS_ENDPOINT_URL_DYNAMODB`/`AWS_ENDPOINT_URL_DYNAMODB_STREAMS` when a Lambda references AWS's DynamoDB Local container, and a blanket LocalStack overwrite redirects that helper to a backend where its table does not exist — an event source that dies silently. WS2's DynamoDB Streams configurator adopts a don't-clobber guard (write service-specific endpoints only when absent). WS3's native `AWS_ENDPOINT_URL_*` emission must adopt the same contract package-wide: **LocalStack values are defaults, never overrides** — anything already present (upstream emulator wiring or user `WithEnvironment`) wins.
- **Attachment is type-name-blind.** `UseLocalStack()` attaches LocalStack to AWS helper executables purely by type name, with no knowledge of which backing store the helper actually targets. In mixed scenarios this still produces a spurious `WaitFor(localstack)` (plus dashboard reference noise) on helpers that never talk to LocalStack. Harmless today but wrong; WS3's design should make attachment target-aware (resolve the helper's backing resource) or at least opt-out-able, rather than scan-and-attach-everything.

### WS4 — Bugs & correctness · P2

- **#24** — `LOCALSTACK_HOST` conflates host port with internal port. In `LocalStackConnectionStringAvailableCallback.cs:40` it is set to the host-facing `{host}:{port}`; when a custom host `Port` is pinned, internal consumers (awslocal, health checks) must still target the internal port `4566`. Tied to todo #7. WS10 research (2026-07-04): the modern fix is network-context-aware endpoint resolution — 13.x `NetworkIdentifier`/`KnownNetworkIdentifiers` + the container tunnel (default since 13.3) give per-network endpoint answers (`LocalhostNetwork` vs `DefaultAspireContainerNetwork`); docker.sock-spawned Lambda containers stay outside Aspire's model and need explicit gateway addressing. See the WS10 research doc.
- **Image-tag / docs drift** — default tag is `4.12.0` (`LocalStackContainerImageTags.cs`) while README/CONFIGURATION examples say `4.10.0`.

### WS5 — Test integrity · P2

- **Complete `LocalStackConnectionStringAvailableCallbackTests`** — partially covered (one real CDK-branch test exists, and env injection is covered in `LocalStackResourceConfiguratorTests`); still untested through the callback: SQS/project branch dispatch and the `LOCALSTACK_HOST` assignment. todo #8.
- **De-flake integration tests** — replace fixed `Task.Delay(10s)` waits in the Lambda functional tests with polling / `WaitForResourceHealthyAsync` or AWS-state polling.
- **Slow tests in a separate collection** — todo #13.
- WS10 research (2026-07-04): document the canonical consumer-test pattern (`WaitForResourceHealthyAsync("localstack")` + AWS SDK asserts, no hardcoded 4566 — testing builder randomizes proxied ports) and verify persistent-lifetime containers behave under 13.2 `--isolated` parallel AppHosts (conflict risk — recommend session lifetime in tests).
- **CDK bootstrap & error-path coverage**; review unit tests (todo #11), enrich integration tests (todo #12).
- **Guard tests for reflected AWS types** — todo #14; ties to WS2's string-typename fragility. (Note: a clean `typeof()` replacement may be impossible because those AWS types are `internal` — confirm in WS2.)
- Decide on the failing SQS-event-source emulator tests: real LocalStack limitation → documented skip, or fixable.

### WS6 — Refactoring / API quality · P2

- `LocalStackContainerOptions` immutability (todo #4) — tension with the `configureContainer` mutation pattern; design call needed.
- `UseLocalStack` mutates `builder.Resources` via Remove/Insert to order CDK bootstrap — works but fragile. WS10 research (2026-07-04): the modern replacement is the eventing model — fluent `On*` callbacks, `IDistributedApplicationEventingSubscriber`, and the 13.3 BeforeStart pipeline phase (`SubscribeBeforeStart`) for deterministic rewiring order; `OnResourceStopped` could also clean up docker.sock-spawned Lambda containers. Evaluate `WithHttpHealthCheck`/`WithHttpProbe` vs the bespoke health check (keep per-service fidelity).
- Post-WS2 helper-resource cleanup — SQS and DynamoDB Streams support will share the same string-matched internal AWS helper-resource pattern across `UseLocalStack()`, `LocalStackConnectionStringAvailableCallback`, constants, and configurators. Consider a small internal abstraction instead of adding more hardcoded branches. WS2 insights (2026-07-04): (a) the two dispatch chains must be edited in lockstep for every new helper type — proven twice now (SQS, DynamoDB Streams); the abstraction should carry, per helper, the type-name constant, the env emission, and whether reference/wait attachment applies. (b) Env emission is inconsistent: the SQS configurator overwrites `AWS_ENDPOINT_URL` unconditionally while the DynamoDB Streams configurator guards service-specific endpoints against pre-existing values (don't-clobber). Latent rather than broken for SQS (no SQS local-emulator variant exists), but the abstraction should unify on guarded emission as the shared contract.
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

Research is complete as of 2026-07-04 (two adversarially cross-checked passes: ecosystem/community process + Aspire 9.0→13.4 feature-evolution sweep; ranked top-10 adoption candidates in the research doc). Adoption mechanisms are routed into WS3/WS4/WS5/WS6/WS7/WS8 bullets; WS10 retains the ATS/polyglot export work, the TS validation AppHost, and the catalog strategy decision (CommunityToolkit re-homing vs upstream filter-widening issue vs documented manual flows — Deniz's call).

Sequencing: land WS2 first; coordinate the ATS-export surface with WS3's API shape; helper-hiding/dashboard UX stays in WS6/WS8.

### WS9 — Docs & internal-docs consolidation · P0 (light, do early)

- ✅ Consolidated `internal-docs/` into this roadmap and deleted the folder (gitignored/historical; all live items captured in the triage table and workstreams). Raw notes now go to the Inbox section below instead of `internal-docs/todo.md`.
- Remaining: fix doc drift surfaced in WS4 and `KNOWN_ISSUES.md`.

## todo.md Triage

Status: ✅ understood · ⚠️ partial · ❓ unclear

| # | Item | One-line meaning | Status | Workstream |
|---|------|------------------|--------|------------|
| 1 | AWS resources on Aspire dashboard | Surface CF stacks/lambdas/buckets/queues/tables as dashboard child-resources. | ✅ | WS8 |
| 2 | Redirect `awsdynamodblocal` to LocalStack | Actually: optionally let users use AWS's official `AddAWSDynamoDBLocal` (native `AWS_ENDPOINT_URL_DYNAMODB`) for DynamoDB while LocalStack serves the rest. | ✅ | WS3 (sub-case) |
| 3 | Make `CreateServiceConfig<T>()` public + reflection | LocalStack.Client `Session`/`SessionReflection` builds per-service `ClientConfig` via reflection; original intent lost and likely moot after WS3. **Parked.** | ❓ | WS3 (park) |
| 4 | `LocalStackContainerOptions` immutable? | Make it init-only/record — conflicts with `configureContainer` mutation; design call. | ✅ | WS6 |
| 5 | LocalStack debugging support | Lambda debugging (attach debugger to Lambda in LocalStack). | ✅ | WS7 |
| 6 | LocalStack Pro features | Research which Pro features we can support natively. | ⚠️ | WS7 |
| 7 | Option host/port should not be used | Container endpoint comes from Aspire; `Config.LocalStackHost/EdgePort` must not be source of truth. Same root as #24. | ✅ | WS3/WS4 |
| 8 | Complete `...CallbackTests` | Stub confirmed; test the real callback behavior. | ✅ | WS5 |
| 9 | `.WithHttpEndpoint(port: options.Config.EdgePort...)` | Wire host port from client config EdgePort — **superseded by #7 / WS3 decoupling. Recommend drop.** | ⚠️ | (drop) |
| 10 | Remove options from `AddLocalStack` | Drop `ILocalStackOptions?` param (mark `[Obsolete]`). | ✅ | WS3 |
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
| Temporary direct `AWSSDK.Core` / `MessagePack` pins | WS3 (decoupling should remove) |

## Open GitHub Issues Mapping

| Issue | Summary | Disposition |
|-------|---------|-------------|
| #12 | `IResourceWithEndpoints` / native `AWS_ENDPOINT_URL_*` so no client dep needed | WS3 |
| #24 | `LOCALSTACK_HOST` port mismatch with custom port | WS4 |
| #25 | Single-image `LOCALSTACK_AUTH_TOKEN` requirement | WS7 (urgency raised — transition live since 2026-03-23; workaround exists) |
| #26 | SES v1 needs Pro; no public container exposure | WS8 (escape hatch) + WS7 (Pro) |
| #18 | LocalStack for Azure | **Out of scope** (future, large) |

## Out Of Scope (for this release)

- **#18 LocalStack for Azure** — large, separate effort.

## Open Questions

- **#9** defaulted to "drop" (superseded by #7/WS3) — confirm.
- **WS6 / `LocalStackContainerOptions` immutability** — resolve the design tension with the mutation-based `configureContainer` callback.

## Inbox / Untriaged

Drop raw, unsorted ideas here as they come up, then triage them into a workstream (and remove them from this list) during roadmap grooming. This replaces the retired `internal-docs/todo.md` capture spot.

- Discoverability: `aspire integration list` / Aspire MCP `list_integrations` catalogs official + CommunityToolkit packages only — `LocalStack.Aspire.Hosting` is not listed (checked 2026-07-04). Root cause found same day (hardcoded ID-prefix filter) — folded into WS10; the remaining open item is Deniz's catalog strategy decision.
- Hybrid real-AWS mode idea (WS10 sweep): model real AWS endpoints as `AddExternalService()` resources so playgrounds can flip LocalStack ↔ real AWS per resource.
- Watch LocalStack platform for an MCP endpoint in the container image; if it ships, annotate with `WithMcpServer(path)` (13.2) so agents auto-discover it.
- Optional deliberate opt-in: `PublishAsDockerComposeService()` passthrough for teams that want LocalStack in generated compose for CI (docker.sock + privileged); default stays `ExcludeFromManifest`.
- Upstream issue candidates from WS2 runtime verification (2026-07-04, evidence in the WS2 design doc): (a) aws-lambda-dotnet — Lambda Test Tool's bundled AWSSDK.Core 4.0.7.x loses the signing region when `AWS_ENDPOINT_URL*` is set (empirical matrix captured; breaks non-us-east-1 LocalStack); (b) aws integrations — API Gateway emulator route config is one-per-Lambda-resource, second `WithReference` silently overwrites the first.
- Consumer guidance to document (WS3/WS9): presigned URLs via LocalStack.Client require `AddAwsService<T>(useServiceUrl: true)` — default proxy-mode registration leaks the AWS regional host into generated URLs.
