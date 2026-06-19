---
name: aspire-source-navigation
description: Use when working on Aspire.Hosting.LocalStack source compatibility, Aspire/AWS/LocalStack package internals, Directory.Packages.props version bumps, or AddLocalStack/UseLocalStack/WithReference behavior.
---

# Aspire Source Navigation

## Overview

This repository maintains an Aspire hosting integration package. Compatibility-sensitive work depends on this repo's package versions and on matching upstream source checkouts, not on memory or upstream default branches.

Use source evidence before editing compatibility-sensitive code. Keep this skill version-light: package versions and concrete refs belong in `Directory.Packages.props`, local `external/` checkouts, and the upstream repositories.

The expected outcome is a short evidence trail: exact package versions, verified upstream refs or an explicit missing-source note, source locations checked, and the compatibility conclusion that drives the change.

## When To Use

Use this skill for work involving:

- `Aspire.Hosting` or `Aspire.Hosting.AWS` internals
- LocalStack.Client behavior
- `Directory.Packages.props` Aspire, AWS integration, or LocalStack client versions
- `AddLocalStack`, `UseLocalStack`, `.WithReference(localstack)`, endpoint/configuration flow, manifest behavior, CloudFormation/CDK, Lambda, or AWS SDK wiring
- String-based references to upstream AWS Aspire integration types
- Reviews of Aspire hosting compatibility or package-version drift

Do not use this skill for ordinary Markdown edits, general C# cleanup, or playground-only work that does not depend on Aspire/AWS/LocalStack internals.

## Required Workflow

1. Read `Directory.Packages.props` and identify the exact package versions involved.
2. Map the packages to their upstream repositories: Aspire packages to `dotnet/aspire`, `Aspire.Hosting.AWS` to `aws/integrations-on-dotnet-aspire-for-aws`, and LocalStack packages to `localstack-dotnet/localstack-dotnet-client`.
3. Check whether a matching local checkout exists under `external/`.
4. Verify the local checkout's branch/tag/commit against the package version and upstream tags/releases before trusting it.
5. If local source is missing or stale, report that explicitly. Use GitHub MCP only for tag/ref discovery, release verification, or targeted fallback reads.
6. Search upstream source for the exact symbols, annotations, extension methods, and behavior involved in the task. Do not rely on pre-baked search terms.
7. Cross-check this repository's implementation and tests against the verified upstream source.
8. Report evidence with file paths and refs before recommending or making changes.

## Package-To-Source Map

Resolve package versions from `Directory.Packages.props` each time. Do not copy versions into this skill.

| Package or behavior | Upstream source | Local checkout root |
| --- | --- | --- |
| `Aspire.Hosting`, `Aspire.Hosting.AppHost`, `Aspire.Hosting.Testing` | `dotnet/aspire` | `external/aspire/{ref}/` |
| `Aspire.Hosting.AWS`, CloudFormation, CDK, Lambda emulator integration | `aws/integrations-on-dotnet-aspire-for-aws` | `external/aws-integrations/{ref}/` |
| `LocalStack.Client`, `LocalStack.Client.Extensions`, `ILocalStackOptions`, session/config options | `localstack-dotnet/localstack-dotnet-client` | `external/localstack-dotnet-client/{ref}/` |

When a task spans multiple packages, verify every involved source. Example: `UseLocalStack()` with Lambda SQS event sources usually involves this repository, `Aspire.Hosting.AWS`, and possibly LocalStack client configuration behavior.

## Local Checkout Layout

Use this layout when local source is available:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/localstack-dotnet-client/{ref}/
```

`{ref}` should be derived from the package version and verified against upstream tags/releases. The `external/` tree is ignored by git. Do not commit upstream source checkouts.

## Ref Verification

Before trusting local upstream source:

1. Read the package version from `Directory.Packages.props`.
2. Inspect the local checkout's current ref using git metadata.
3. Verify that ref against upstream tags, release branches, or commits for the package version.
4. If the mapping is not obvious, say so and use a targeted upstream lookup to establish the mapping.

Acceptable evidence includes a tag name, release branch, commit SHA, or upstream release page that ties the package version to the source. Unacceptable evidence includes repository default branches, approximate version names, or unchecked local folder names.

## Missing Or Stale Source

If the matching local checkout does not exist, do not silently continue with default-branch source. Report the gap before making compatibility-sensitive conclusions.

Use this wording pattern:

```text
Upstream source status:
- Aspire.Hosting {version}: no matching local checkout under external/aspire/{ref}; using targeted GitHub fallback for {symbols/files} only.
- Aspire.Hosting.AWS {version}: local checkout {path} verified at {ref-or-sha}.
- LocalStack.Client {version}: not involved in this change.
```

Create or refresh `external/` checkouts only when the user has approved that setup work or when the current task explicitly includes source setup. Keep those checkouts uncommitted.

## Evidence Report

Before recommending or making changes, provide the evidence in this shape:

```text
Compatibility evidence:
- Package versions: Aspire.Hosting {version}, Aspire.Hosting.AWS {version}, LocalStack.Client {version-or-not-involved}.
- Upstream refs checked: {repo}@{ref-or-sha}, ...
- Upstream files/symbols checked: {file}:{symbol}, ...
- Repo files/tests checked: {file}:{symbol-or-test}, ...
- Conclusion: {what changed, what is compatible, what is risky, or what remains unverified}.
```

Keep the report short, but include enough detail that another agent can reproduce the source lookup.

## Search Guidance

Search by the exact behavior under review:

- Extension methods: `AddLocalStack`, `UseLocalStack`, `WithReference`, `WithEnvironment`, `WaitFor`, `ExcludeFromManifest`.
- Aspire resource model: annotations, `IResourceWithEnvironment`, `IResourceWithWaitSupport`, endpoint references, connection string callbacks, manifest publishing.
- AWS integration: CloudFormation resources, CDK stacks/bootstrap, Lambda emulator resources, SQS event source resources, output/reference annotations.
- LocalStack client: `ILocalStackOptions`, `LocalStackOptions`, `SessionOptions`, `ConfigOptions`, `AddLocalStack`, `AddAwsService`, environment variable binding.

These are starting points, not a fixed checklist. Add or remove searches based on the concrete task.

## Official Aspire Skills Cross-Check

Official Microsoft Aspire skills are useful for Aspire CLI and distributed application workflows, but they may describe newer Aspire versions than this repository uses. If official guidance conflicts with verified package source, prefer verified package source and call out the version mismatch.

Use official skills when available for:

| Task | Skill |
| --- | --- |
| AppHost lifecycle | `aspire` or `aspire-orchestration` |
| Logs, dashboard, traces | `aspire-monitoring` |
| AppHost scaffold/resource graph work | `aspireify` |
| New skeleton creation | `aspire-init` |
| Publish/deploy/destroy | `aspire-deployment`, approval-gated |

Installed local skills can supplement this one:

| Task | Skill |
| --- | --- |
| Explicit configuration and env vars | `aspire-configuration` |
| Playground ServiceDefaults | `aspire-service-defaults` |
| `DistributedApplicationTestingBuilder` patterns | `aspire-integration-testing`, adapted to this repo's test framework |

## Common Mistakes

- Do not use upstream default branches for compatibility-sensitive source checks.
- Do not assume package versions map directly to semver git tags; verify the upstream tag/release scheme.
- Do not copy examples from external testing guidance without adapting them to this repo's test framework.
- Do not commit `external/` source checkouts.
- Do not treat GitHub MCP as the default source-reading path when local source is available.
- Do not claim source compatibility from this repository's tests alone; upstream API shape must be checked for version-sensitive behavior.
- Do not leave the evidence trail implicit in chat history; summarize refs and file paths before the recommendation or edit.
