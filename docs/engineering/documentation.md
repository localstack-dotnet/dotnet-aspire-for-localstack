# Documentation ownership

Date: 2026-11-05

This repository must explain current behavior and accepted decisions without access to a private archive. Each current fact has one owner. Other documents may summarize a fact for their own reader and link to that owner; they must not become a second place to maintain the rule.

## Owners

| Question | Current owner |
| --- | --- |
| How do agents work here, and where do they look first? | [`AGENTS.md`](../../AGENTS.md); `CLAUDE.md` and native skill relays only point to it |
| Why is the hosting wiring shaped the way it is? | [Hosting model](../architecture/hosting-model.md) |
| How does a consumer configure the package? | [Configuration](../CONFIGURATION.md) and the [root README](../../README.md) |
| Which upstream defects affect this package, and what closes them? | [Upstream Watch](../upstream-watch.md) |
| Which words mean what? | [Glossary](../../GLOSSARY.md) |
| What changed in a release? | [Changelog](../../CHANGELOG.md) |
| Which package, SDK, framework, image, and CI values apply now? | `global.json`, `Directory.Build.props`, `Directory.Packages.props`, project files, `LocalStackContainerImageTags.cs`, and workflows. Prose does not copy these values. |
| What is in progress, next, deferred, or undecided? | The [Aspire Hosting roadmap](https://github.com/localstack-dotnet/.github/blob/main/roadmap/aspire-hosting.md) in `localstack-dotnet/.github` |
| What did a past investigation observe, and what did a plan propose? | Dated evidence under `products/aspire-hosting/` in the private `localstack-dotnet/localstack-internal` |

## Companion repositories

Documentation is part of the change, not follow-up work. Aspire Hosting's documentation spans three repositories, and a change keeps each of them current:

- **This repository** holds current canon and consumer documentation. Update it in the same pull request as the behavior, topology, package support, or agent expectation it describes.
- **`localstack-dotnet/localstack-internal`** (private) holds dated research, design and implementation plans, handoffs, and roadmap snapshots under `products/aspire-hosting/`, with an `inventory.md`. Add new evidence there, under that repository's ingestion policy, in the same working session as the change it supports. Archived text is evidence: it is never current policy, open work, or approval.
- **`localstack-dotnet/.github`** holds the [Aspire Hosting roadmap](https://github.com/localstack-dotnet/.github/blob/main/roadmap/aspire-hosting.md) and the organization roadmap. When a workstream starts, lands, or is deferred, update the roadmap in a companion pull request.

Canon here cites private evidence by date and title, for example: internal evidence, 2026-10-16, "Aspire/AWS evolution research". Evidence that was public before 2026-11-05 may instead be cited by a permalink pinned to its last public revision. Never link a private repository path from canon, and never move a fact that canon needs into the private archive.

## Lifecycles

- **Current architecture, configuration, and Upstream Watch** describe the checked-in behavior and change with it. A `Date:` line is the latest substantive update.
- **Research** is dated evidence and may describe superseded states. Name it `YYYY-MM-DD-<topic>.md` in the archive.
- **Plans and handoffs** are temporary. Retire them after their durable outcome reaches canon, the changelog, or the roadmap.
- **Release history** belongs to the changelog. A merged change is not a published package.

## Writing rules

- Prefer the existing natural owner to a new document.
- Moving documentation is relocation, not summarization: keep unique claims, dates, commits, measurements, and rejected alternatives in their new owner or in dated evidence before deleting the old text.
- Code comments and XML docs stand on their own, without reaching for a spec, plan, phase, or file path.
