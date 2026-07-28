---
name: git-workflow
description: Git commit and branch conventions for the data-catalogue-upload repository. Use when staging changes, writing commit messages, or creating branches. Covers Conventional Commits, branch naming, the pre-commit quality checklist (dotnet format/build/test), and the Co-Authored-By trailer.
---

# Git workflow (data-catalogue-upload)

## Commit messages: Conventional Commits

Format: `type(optional-scope): short imperative summary`

Common types:

| Type | Use for |
|------|---------|
| `feat` | new functionality |
| `fix` | bug fix |
| `chore` | tooling, deps, config, repo housekeeping |
| `refactor` | code change with no behavior change |
| `test` | adding or fixing tests |
| `docs` | documentation only |

Useful scopes for this repo: `sync`, `domain`, `infra`, `db`, `migrations`, `api`, `biobank-api`,
`uploader`, `ci`.

Examples:

```
feat(uploader): add radiology imaging-study mapping
fix(sync): count per-entity failures without aborting the run
chore: bump EF Core to 10.0.9
refactor(domain): reshape the biobank domain into DDD aggregates
test(biobank-api): cover the hand-written persistence mapper
docs: document required API URL env vars
```

Keep the summary in the imperative mood and under ~72 characters. Add a body (after a blank line) to explain
the "why" when it is not obvious. This repo's history uses scoped messages like
`refactor(biobank-api): ...` - match that style.

## Branch naming

`type/short-kebab-description`, matching the commit types:

```
feat/radiology-mapping
fix/sync-failure-count
refactor/port-to-csharp
```

Branch off `master`; don't commit directly to it.

## Before committing

Run the same checks CI runs, from the repo root, and make sure they pass:

```bash
dotnet format DataCatalogueUpload.slnx --verify-no-changes   # drop the flag to auto-fix formatting
dotnet build DataCatalogueUpload.slnx -c Release             # warnings are errors
dotnet test DataCatalogueUpload.slnx
```

- Do **not** commit secrets, connection strings, or a real `appsettings.*.local.json` - configuration comes
  from environment variables.
- Do **not** hand-edit NuGet versions onto a `PackageReference`; use central package management
  (`dotnet add <project> package <name>`, which updates `Directory.Packages.props`).
- Don't loosen analyzer/format settings to make the checks pass - fix the code.

## Commit trailer

End commit messages created by the agent with:

```
Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
```
