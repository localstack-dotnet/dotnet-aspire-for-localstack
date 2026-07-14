# WS10 Aspire Ecosystem Integration Research

Date: 2026-07-04 (initial pass); implementation status rechecked 2026-07-08

## Question

Since this project started, Aspire has grown enormously: a CLI-first workflow, TypeScript/polyglot AppHosts, an integration catalog (`aspire add` / `aspire integration list` / MCP `list_integrations`), an agent-tooling story, and a much richer hosting toolbox (dashboard commands/URLs/icons, container files/volumes, parameters + interaction service, eventing, MCP hooks). The question is the full opportunity sweep: **what has changed in the Aspire world, and what new support can this package add?** TypeScript AppHost compatibility and CLI friendliness are two instances of that question, not its boundary.

## Implementation Status Update (2026-07-08)

Conclusion: this research is still active backlog. It has not been implemented, and none of the core concerns have become obsolete.

Evidence checked:

- Package pins remain `Aspire.Hosting`/`Aspire.Hosting.AppHost`/`Aspire.Hosting.Testing` `13.4.6`, `Aspire.Hosting.AWS` `13.3.1`, and `LocalStack.Client` `2.0.0` in `Directory.Packages.props`; the local upstream checkout `external/aspire/v13.4.6` is verified at tag `v13.4.6` (`87fe259e4fc244c599019a7b1304c85a1488f248`), so the Aspire-source claims below still match the pinned core package.
- `src/Aspire.Hosting.LocalStack/Aspire.Hosting.LocalStack.csproj` has no `<EnableAspireIntegrationAnalyzers>` property, no analyzer package reference, and no `polyglot` package tag.
- `src/Aspire.Hosting.LocalStack/LocalStackResource.cs` still implements `ContainerResource, ILocalStackResource`; it does not implement `IResourceWithCustomWithReference<LocalStackResource>`.
- `src/Aspire.Hosting.LocalStack/LocalStackProjectExtensions.cs` still exposes the custom C# `WithReference` extension with `IResourceWithEnvironment` and `IResourceWithWaitSupport` constraints, so TS/polyglot `withReference(localstack)` still needs the custom-resource hook described below.
- No TypeScript validation AppHost or `aspire.config.json` package-reference-mode sample exists under `playground/`.
- Dashboard/catalog polish remains routed to WS6/WS8; the package source still does not use `WithHidden`, `WithCommand`, `WithUrls`, `WithIconName`, or `ExcludeFromMcp`.

Status call: keep this document as active research/backlog. In the roadmap, WS10 should be `Planned`, not `Researching`: the research phase is complete, while implementation waits on WS3A API-shape decisions plus Deniz's catalog strategy decision.

## Source Evidence (verified against `external/aspire/v13.4.6`, tag == HEAD `87fe259`)

### 1. TypeScript/polyglot AppHost support for third-party integrations is official and attribute-based

- `src/Aspire.Hosting/Ats/ThirdPartyAtsAttributes.md` is an in-repo authoring guide **specifically for third-party integration authors**. The ATS (Aspire Type System) capability scanner discovers attributes by **full type name** (`Aspire.Hosting.AspireExportAttribute`, `AspireExportIgnoreAttribute`, `AspireDtoAttribute`, `AspireUnionAttribute`), so packages that already reference `Aspire.Hosting` (we do, 13.4.6) can use the real attributes directly.
- Export model: `[AspireExport("addLocalStack")]` on builder extension methods (capability ID = `{AssemblyName}/{id}`, camelCase-derived when omitted), `[AspireExport]` on resource types, `ExposeProperties`/`ExposeMethods` for context/callback types, `[AspireDto]` for options records, `[AspireUnion]` for union-typed parameters, `[AspireExportIgnore]` to exclude members.
- `Aspire.Hosting.Integration.Analyzers` (ships on NuGet, appears in the integration catalog) provides the same authoring diagnostics used by in-repo Aspire integrations — reference with `PrivateAssets="all"`.
- **Custom `WithReference` semantics do not flow to polyglot AppHosts automatically.** The polyglot `withReference` goes through an internal object-typed dispatcher (`ResourceBuilderExtensions.TryDispatchCustomWithReference`) that consults `IResourceWithCustomWithReference<TSelf>` via reflection; the public strongly-typed C# `WithReference` overloads (including this package's `LocalStackProjectExtensions.WithReference`) are annotated `[AspireExportIgnore]` upstream and are never called from ATS. A TS AppHost calling `withReference(localstack)` today would get Aspire's default connection-string injection — **not** this package's `LocalStackEnabledAnnotation`/`WaitFor`/bidirectional-reference wiring. The fix is implementing `IResourceWithCustomWithReference<LocalStackResource>` with a static `TryWithReference` that routes to our extension; `QdrantServerResource` (`src/Aspire.Hosting.Qdrant/QdrantServerResource.cs:105-128`) is the canonical in-repo reference implementation, including `[AspireExport(ExposeProperties = true)]` on the resource class.
- Caveat for our API shape: `TryWithReference<TDestination>` constrains `TDestination : IResourceWithEnvironment` only; our custom `WithReference` also requires `IResourceWithWaitSupport`. The implementation needs a runtime capability check instead of the compile-time constraint.
- `UseLocalStack()` (an `IDistributedApplicationBuilder` extension) is exportable as a plain capability method the same way.

### 2. The integration catalog gate is a hardcoded package-ID prefix — we are invisible and uninstallable via `aspire add`

- `src/Aspire.Cli/Packaging/PackageChannel.cs:541` (`IsIntegrationPackageId`): a package is an "integration" iff its ID starts with `Aspire.Hosting.` or `CommunityToolkit.Aspire.Hosting.` (minus excluded infrastructure packages). The underlying feed query is a NuGet search for `Aspire.Hosting` (`NuGetPackageCache.GetIntegrationPackagesAsync`).
- Our package ID is **`LocalStack.Aspire.Hosting`** → fails the prefix check → never appears in `aspire integration list`, `aspire add` prompts, or MCP `list_integrations` (empirically confirmed 2026-07-04: the MCP catalog shows official + CommunityToolkit only).
- **Exact-ID `aspire add LocalStack.Aspire.Hosting` also fails**: `AddCommand` (line 175) matches friendly name / exact ID **within the pre-filtered candidate set**, so packages outside the prefix convention cannot be force-added. `CommunityToolkitFirstComparer` further confirms CommunityToolkit is the blessed community channel.
- The `Aspire.Hosting.*` NuGet prefix is Microsoft-reserved, so renaming our package into the official prefix is not an option.
- Consequences by AppHost flavor (web-verified 2026-07-04):
  - **Classic csproj AppHost**: unaffected — users `dotnet add package LocalStack.Aspire.Hosting` as today.
  - **Single-file `apphost.cs`**: works — `#:package <Id>@<version>` is a generic .NET 10 SDK feature with no Aspire gate; `#:package LocalStack.Aspire.Hosting@x.y.z` is valid today.
  - **TypeScript AppHost**: `aspire add` will not list or install the package (and for non-C# AppHosts it additionally filters to the `polyglot` NuGet tag unless `--all` is passed). **But SDK codegen itself accepts arbitrary package IDs**: the official multi-language authoring guide's own examples use `MyCompany.Hosting.MyDatabase` in `aspire.config.json`'s `packages` section. So TS users can hand-add the package to `aspire.config.json` and run `aspire restore` — supported, just not discoverable/automated.

### 3. What "CLI/dashboard/MCP friendly" already works, verified live 2026-07-04

Running the Lambda playground under `aspire start` + Aspire MCP: full resource graph visible including this package's LocalStack container with its `localstack_health` health report, helper-resource env keys (values hidden), `WaitFor`/`Reference` relationships, per-resource console-log search. No changes needed for baseline observability. Known cosmetic gaps: helper resources (CDK bootstrap, event-source pollers) are not `WithHidden()` (WS6/WS8), and resources expose no custom dashboard commands or icons.

### 4. Capability-adoption gap analysis: the modern hosting toolbox vs. what this package uses

All APIs below verified present in the `Aspire.Hosting` 13.4.6 public surface (`src/Aspire.Hosting/api/Aspire.Hosting.cs`). "Package today" reflects `src/Aspire.Hosting.LocalStack` as of this pass.

| Capability | Package today | Adoption idea | Ties to |
| --- | --- | --- | --- |
| `WithHidden()` / `WithHiddenOnCompletion()` | Not used | Hide implementation-detail helpers (CDK bootstrap, event-source pollers) from dashboard/CLI/MCP default views | WS6/WS8 (already flagged) |
| `WithCommand(...)` custom resource commands | Not used | Commands on the `localstack` resource, e.g. "Open LocalStack Web App" (app.localstack.cloud attaches to a local instance), "Open health endpoint" | WS8 |
| `WithUrls` / `WithUrlForEndpoint` | Not used | Surface `/_localstack/health` and the LocalStack Web App link as named dashboard URLs (also improves MCP `list_resources` output) | WS8 |
| `WithIconName(...)` | Not used | Distinct icon for the LocalStack resource instead of the generic container glyph | WS8 (cosmetic) |
| `WithContainerFiles(...)` | Not used | Ship init scripts into `/etc/localstack/init/ready.d` without user-managed bind mounts — the cleanest answer to issue #26's "escape hatch / provisioning hook" | WS8 (#26) |
| `WithVolume(...)` | Only docker.sock bind mount | Optional named volume for `/var/lib/localstack` (persistence scenarios; interacts with streams-persistence caveat) | WS7 |
| `AddParameter(..., secret: true)` + `IInteractionService` | Not used | `LOCALSTACK_AUTH_TOKEN` as a secret parameter with interactive prompt on first run — the idiomatic WS7 unified-image story instead of raw `AdditionalEnvironmentVariables` | WS7 (#25) |
| `ExcludeFromMcp()` | Not used | Decide per helper resource whether agent visibility is signal or noise | WS8/agents |
| Eventing (`OnResourceReady`, `BeforeStartEvent`, subscriber model) | Only `OnConnectionStringAvailable` | A public "LocalStack ready" hook so consumers can run provisioning (SES verify, seed data) — second half of issue #26 | WS8 (#26) |
| `WithHttpHealthCheck` | Custom `/_localstack/health` check (service-aware) | Keep ours — it is strictly better; no action | — |
| `WithExplicitStart`, `WithProcessCommand`, `PublishAsConnectionString` | Not used | No compelling use; publish behavior already handled via `ExcludeFromManifest` (verify under 13.x Docker/K8s publishers — pending web) | — |
| Container tunnel (`ASPIRE_ENABLE_CONTAINER_TUNNEL`, 13.x networking) | Untouched | Verify whether tunnel semantics change host-vs-container reachability of the LocalStack endpoint (interacts with `LOCALSTACK_HOST` port conflation bug) | WS4 (#24) |

## Web-Verified Findings — Pass 1: Ecosystem And Community Process (2026-07-04)

Verified via aspire.dev docs, microsoft/aspire and CommunityToolkit/Aspire sources. Key URLs: `aspire.dev/extensibility/multi-language-integration-authoring/`, `CommunityToolkit/Aspire/blob/main/docs/create-integration.md`, `microsoft/aspire/blob/main/src/Aspire.Cli/NuGet/NuGetPackageCache.cs`, `microsoft/aspire/tree/main/.agents/skills/hosting-integration-authoring`.

### Catalog and discoverability

- There is **no curated feed, registration JSON, or qualifying NuGet tag** for the integration catalog. It is a live NuGet search (`dotnet package search "Aspire.Hosting"`) filtered by the hardcoded two-prefix allow-list confirmed in the source evidence above. The prefix rule is publicly undocumented; it appears only in code and in microsoft/aspire's internal `hosting-integration-authoring` agent skill (written for first-party authors).
- `Aspire.*` is a **reserved NuGet prefix**, so renaming into `Aspire.Hosting.LocalStack` is impossible for non-Microsoft owners.
- **CommunityToolkit is the only community path into the catalog** (and into the VS "Add Aspire Integration" dialog and aspire.dev docs). The process is a code contribution **into their repo** (`CommunityToolkit/Aspire` → `src/CommunityToolkit.Aspire.Hosting.LocalStack`): proposal issue first, then code under their CI/governance (.NET Foundation, MIT), xunit test project, example AppHost, packed README, docs PR to microsoft/aspire.dev. There are no defined "donate an existing package" mechanics — in practice existing packages get re-implemented there and the original is deprecated with a pointer. That is a re-homing decision, not a listing.
- Upstream door is ajar for a proposal: the CLI's own tests exercise friendly-name handling for `Acme.Aspire.Hosting.Foo.Bar`-shaped vendor IDs, so the architecture does not preclude widening the filter — there is simply no mechanism today. Filing a microsoft/aspire issue proposing vendor-prefix or opt-in listing is a low-cost, non-blocking move.

### Polyglot authoring (official, for arbitrary packages)

- The official guide is **"Multi-language integrations"** (`aspire.dev/extensibility/multi-language-integration-authoring/`), explicitly written for third-party packages. Mechanism: the CLI scans the integration assembly for ATS attributes (`[AspireExport]`, `[AspireDto]`, `[AspireUnion]`, `[AspireValue]`, `[ResourceName]`, `[AspireExportIgnore]`), generates a typed TS SDK under `.aspire/modules/`, and calls the unmodified C# over JSON-RPC at runtime. XML doc comments become JSDoc (`<ats-summary>` overrides supported).
- Build-time validation: `<EnableAspireIntegrationAnalyzers>true</EnableAspireIntegrationAnalyzers>` (or standalone `Aspire.Hosting.Integration.Analyzers`); diagnostics `ASPIREEXPORT001–017`; "a clean build with zero analyzer warnings or errors is the required baseline."
- **The `polyglot` NuGet tag is auto-added at pack time** when the analyzers property is enabled (via `Aspire.Hosting`'s buildTransitive targets; opt-out `<IsAspirePolyglotCompatible>false</IsAspirePolyglotCompatible>`). Non-C# `aspire add` filters on that tag — so shipping it is correct hygiene even while the prefix filter keeps us out of the list.
- Local dev loop: `aspire.config.json` `packages` can point at a **`.csproj` path** (project-reference mode) — the CLI builds it and regenerates the SDK. This is how we would test a TS AppHost against the package in-repo.
- **One ATS annotation investment covers future languages**: Python/Go/Java/Rust AppHosts exist in main-branch tests and on the public roadmap; the same metadata drives all generated SDKs. (Python-as-workload is already GA and unrelated.)
- API-shape warning: callback-taking or SDK-type-taking surfaces (`Action<LocalStackContainerOptions>`, `IAWSSDKConfig`, `ILocalStackOptions`) need ATS-first adaptation — DTO options bags, `[AspireExportIgnore]` on C#-only overloads. This couples directly to WS3A's API reshaping and WS6's options-immutability decision.

### Not extensible today (accept and route around)

- `aspire new` templates: CLI hardwires `Aspire.ProjectTemplates`; community templates work via `dotnet new install` but the aspire CLI won't discover them.
- `aspire docs` / `aspire docs api`: official aspire.dev content only; no third-party indexing.
- Agent skills: no NuGet/package-shipped skill discovery convention; `aspire agent init` installs only the official bundle. Practical route: a strong NuGet README (what agents actually read) plus, if desired, our own agent-plugin repo in the microsoft/aspire-skills pattern.
- `ConfigurationSchema.json` appsettings IntelliSense: works for any package via `JsonSchemaSegment` MSBuild items, but the mechanism is only documented in GitHub discussions — viable, low priority.

## Web-Verified Findings — Pass 2: Feature-Evolution Sweep, Aspire 9.0 → 13.4 (2026-07-04)

Method: what's-new pages 9.0–9.5 and 13.0–13.4 plus container-networking/publishing/authoring docs, with every 13.x API claim cross-verified against the local `external/aspire/v13.4.6` API baseline. Experimental gates confirmed there: `ASPIREINTERACTION001` (InteractionService), `ASPIREPIPELINES001`, `ASPIREUSERSECRETS001`, `ASPIREPERSISTENCE001`, `ASPIREPROCESSCOMMAND001`.

**Corrections applied during synthesis** (where the sweep conflicted with Pass 1's source-verified findings):

- NuGet tags (`aspire`, `integration`, etc.) do **not** get a package into `aspire integration list/search` — the hardcoded ID-prefix filter governs. The only tag that matters is the auto-added `polyglot` tag, and only within the already-cataloged set.
- "Mandatory 13.0 networking migration" does not apply: this package builds clean against 13.4.6 and never used `containerHostName`/lifecycle hooks. The real item is *adopting* network-context-aware endpoint resolution (below), not fixing a break.

### Additions beyond the gap-analysis table (§4 above)

| Capability | Since | Adoption idea | Route to |
| --- | --- | --- | --- |
| Container tunnel (default since 13.3) + `NetworkIdentifier`/`KnownNetworkIdentifiers` + network-context-aware `EndpointReference` | 13.0–13.3 | The correctness foundation for LocalStack's defining topology problem: host clients, Aspire-managed container clients, and docker.sock-spawned Lambda containers all need a *different* answer for "where is LocalStack". Resolve endpoints per network context (`LocalhostNetwork` vs `DefaultAspireContainerNetwork`) instead of a single host-facing string; docker.sock-spawned containers remain outside Aspire's model and still need explicit gateway addressing | WS4 (#24) / WS3B |
| `WithHttpCommand()` | 9.2 | One-click `POST /_localstack/state/reset` (Pro) style commands | WS8 |
| `WithParentRelationship()` nesting (+ 13.4 child/reference relationship APIs) | 9.1 | Nest auto-wired CloudFormation/CDK/Lambda resources under the LocalStack resource in the dashboard — turns `UseLocalStack()` output into a visible "AWS-on-LocalStack" control panel | WS8 |
| `WithImagePullPolicy()` (`Never` since 13.2) | 9.2 | Expose on container options for air-gapped CI and `latest`-tag users | WS7 |
| `ExcludeReferenceEndpoint` on `EndpointAnnotation` | 13.3 | If a second endpoint (HTTPS/metrics) is ever added, keep consumer env injection 4566-only | WS3A |
| `WithHttpsDeveloperCertificate()` / `WithCertificateTrustConfiguration()` | 13.1/13.2 | Optional HTTPS LocalStack endpoint + trusting it from client containers (some AWS SDK flows insist on https) | WS7/WS8 backlog |
| Connection properties: `WithConnectionProperty()`, standardized names | 13.0/13.1 | Expose `EndpointUrl`/`Region`/`AccessKey`/`SecretKey` as structured, language-agnostic properties — the polyglot consumer story beyond .NET | Unassigned; WS3A does not add endpoint emission |
| Named references `WithReference(resource, "name")` | 13.0 | Deterministic env-prefix control for consumers | WS3A |
| Fluent eventing (`OnResourceReady`, `OnBeforeResourceStarted`, `OnResourceStopped`) + `IDistributedApplicationEventingSubscriber` + BeforeStart pipeline phase (`SubscribeBeforeStart`, 13.3) | 9.4–13.3 | `UseLocalStack()` rewiring in a BeforeStart phase for deterministic ordering (replaces the fragile Remove/Insert resource reordering — WS6 already tracks that smell); `OnResourceStopped` could clean up docker.sock-spawned Lambda containers | WS6 |
| `WithHttpProbe()` (startup/readiness/liveness) | 9.5 | Consider modeling `/_localstack/health` as probes; keep the bespoke service-aware check for per-service status | WS6 (evaluate) |
| `WaitBehavior` overloads | 9.4 | Expose fail-fast vs wait-on-unhealthy as an option | WS6 backlog |
| `AddExternalService()` | 9.4 | Hybrid mode: model real-AWS endpoints as external services so playgrounds can flip LocalStack ↔ real AWS per resource | Inbox (novel feature idea) |
| Dynamic parameter inputs (dropdowns, custom choice) | 13.0 | "Which LocalStack services to eager-load" as a prompted choice list | WS7 backlog |
| `aspire secret` CLI + `IUserSecretsManager` | 13.2 | Document `aspire secret set` for the auth token | WS7 (#25) |
| `aspire exec --resource localstack -- awslocal ...` | 9.5 | Document: injected env means `awslocal`/AWS CLI get `AWS_ENDPOINT_URL` for free | WS9 docs |
| `ConfigurationSchema.json` | convention | Ship for the consumer-side `LocalStack` settings section (IntelliSense in appsettings.json) | WS9/WS3A |
| Testing: `WaitForResourceHealthyAsync` pattern; `--isolated` mode (13.2) | 9.4/13.2 | Document canonical consumer-test pattern; verify persistent-lifetime containers under isolated parallel AppHosts (conflict risk — recommend session lifetime in tests) | WS5 |
| `aspire update` third-party behavior | 13.x | Verify our package gets version-updated in CPM repos without breaking Aspire compat expectations | WS10 verify item |
| `WithMcpServer(path)` | 13.2 | LocalStack ships its own MCP server product — annotate when/if the container exposes an MCP endpoint, so agents auto-discover it | Inbox (watch LocalStack platform) |
| `ResourceCommandVisibility` + typed command args | 13.4 | Expose "reset state" to dashboard but deliberately include/exclude from MCP; typed args ("reset only service X") | WS8 |
| `PublishAsDockerComposeService()` passthrough | 13.1–13.3 | Optional: teams that want LocalStack in generated compose for CI (docker.sock + privileged) — deliberate opt-in, default stays `ExcludeFromManifest` | Inbox |
| `ExcludeFromManifest` in the 13.x publish model | verified | Still the documented, sufficient mechanism for dev-only resources (13.1 fixed excluded-resource deploy failures; watch reference-chain edge cases) | Closes the earlier open question |

### Ranked adoption candidates (both passes merged, corrected)

1. **Network-context-aware endpoints + container tunnel semantics** (13.0/13.3) — correctness foundation for host-vs-container-vs-docker.sock topology; subsumes and modernizes bug #24. → WS4/WS3B
2. **Secret `ParameterResource` + InteractionService prompting for `LOCALSTACK_AUTH_TOKEN`** (9.4+, experimental attr) — turns the WS7 unified-image migration into a zero-docs first-run prompt persisted to user secrets. → WS7
3. **`WithContainerFiles` for `/etc/localstack/init/ready.d`** (9.2) — the clean answer to #26; kills bind-mount friction on Windows/CI. → WS8
4. **Eventing modernization: fluent `On*`, subscribers, BeforeStart pipeline phase** (9.4–13.3) — deterministic `UseLocalStack()` ordering, replacing the Remove/Insert hack. → WS6
5. **Command UX: `WithCommand`/`WithHttpCommand` + logger + visibility + typed args** (9.0→13.4) — "Reset LocalStack state", "Dump diagnostics" as dashboard/CLI/MCP actions; the most visible UX win. → WS8
6. **Connection properties** (13.0/13.1) — structured `EndpointUrl`/`Region`/credentials for non-.NET consumers and agents. WS3A does not emit `AWS_ENDPOINT_URL*`; revisit only if a future native-endpoint feature is approved. → Unassigned
7. **Dashboard topology: parent/child nesting + `WithUrls` + `WithIconName`** (9.1–9.5) — near-zero-effort "AWS-emulation control panel" presentation. → WS8
8. **ATS export + analyzers + `IResourceWithCustomWithReference`** (13.4) — the polyglot reach multiplier; sequence after WS3A's API reshape. → WS10 core
9. **Standard health APIs (`WithHttpHealthCheck`/`WithHttpProbe`) where they don't lose per-service fidelity** (9.0/9.5) — platform-native readiness semantics. → WS6 evaluate
10. **MCP/agent hygiene: `ExcludeFromMcp` on helpers, deliberate command visibility, descriptive URLs** (13.0–13.4) — cheap good-citizenship in the agent-era workflow. → WS8

Explicitly irrelevant (local-dev-only nature): Helm/ingress/registry/deployment-state/`aspire destroy` machinery, `PublishWithContainerFiles`, `WithBuildSecret`, deployment image tags, GenAI visualizer (client-telemetry feature).

## Candidate Work Items (draft, pending prioritization)

| Item | Effort | Value | Depends on |
| --- | --- | --- | --- |
| ATS export pass: `<EnableAspireIntegrationAnalyzers>true</...>` (auto-adds the `polyglot` NuGet tag at pack), `[AspireExport]` on `AddLocalStack`/`UseLocalStack`/resource types, DTO options bags for callback/SDK-typed surfaces, analyzer-clean baseline (`ASPIREEXPORT001–017`) | Medium | Polyglot-exportable package; same metadata covers future Python/Go/Java/Rust AppHosts; analyzers raise API hygiene | Do after WS3A reshapes the public surface (avoid annotating soon-to-change APIs) |
| `IResourceWithCustomWithReference<LocalStackResource>` implementation (Qdrant/AzureFunctions pattern; runtime `IResourceWithWaitSupport` check) + tests | Small | Correct `withReference(localstack)` semantics from TS AppHosts | Nothing upstream |
| TS validation AppHost: `aspire.config.json` project-reference mode pointing at the package `.csproj`, exercising `addLocalStack`/`useLocalStack` from TypeScript | Small-medium | Proves the export surface end-to-end; doubles as a playground | ATS export pass |
| Catalog strategy decision: CommunityToolkit re-homing (`CommunityToolkit.Aspire.Hosting.LocalStack`, code donation into their repo/governance) vs upstream filter-widening issue vs status quo + documented manual flows (`dotnet add package`, `#:package`, `aspire.config.json`) | Decision + advocacy | Discoverability, `aspire add`, VS dialog, aspire.dev docs page | Deniz's ownership preference; upstream issue is low-cost and non-exclusive with the others |
| Dashboard/CLI UX polish: `WithHidden()` for helpers, `WithCommand`/`WithUrls`/`WithIconName` on the LocalStack resource | Small | `aspire ps`/dashboard/MCP output quality | Route into WS6/WS8 — do not duplicate |
| Agent-era onboarding: strong NuGet README (what agents read) ± own agent-plugin repo in the aspire-skills pattern | Small / Medium | Consumer onboarding in agent-driven workflows | No Aspire-native convention exists; independent of everything above |

## Relationship To Existing Workstreams

- WS2 (DynamoDB Streams) is unaffected and proceeds first; ATS export is additive and touches the same public surface, so land WS2 before annotating.
- WS3A interacts: ATS-exported API surface should use the package-owned post-WS3A shape where possible, or exports will need churn.
- WS6/WS8 own helper-resource hiding and dashboard UX; this workstream only flags them as ecosystem-visible.
