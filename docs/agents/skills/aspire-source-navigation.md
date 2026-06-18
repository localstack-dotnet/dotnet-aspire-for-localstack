---
name: aspire-source-navigation
description: Use when working on Aspire.Hosting.LocalStack source compatibility, Aspire/AWS/LocalStack package internals, Directory.Packages.props version bumps, or AddLocalStack/UseLocalStack/WithReference behavior.
---

# Aspire Source Navigation

## Overview

This repository maintains an Aspire hosting integration package. Compatibility-sensitive work depends on this repo's package versions and on matching upstream source checkouts, not on memory or upstream default branches.

Use source evidence before editing compatibility-sensitive code. Keep this skill version-light: package versions and concrete refs belong in `Directory.Packages.props`, local `external/` checkouts, and the upstream repositories.

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

## Local Checkout Layout

Use this layout when local source is available:

```text
external/aspire/{ref}/
external/aws-integrations/{ref}/
external/localstack-dotnet-client/{ref}/
```

`{ref}` should be derived from the package version and verified against upstream tags/releases. The `external/` tree is ignored by git. Do not commit upstream source checkouts.

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
