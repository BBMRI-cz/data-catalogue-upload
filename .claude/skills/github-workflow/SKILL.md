---
name: github-workflow
description: GitHub CI, pull request, and issue conventions for the data-catalogue-upload repository. Use when pushing to GitHub, opening or describing a pull request, filing an issue, or investigating a failing CI run. Covers what CI checks run (dotnet restore/format/build/test), reproducing them locally, fixing common failures, and PR/issue templates plus the project-board workflow.
---

# GitHub workflow (data-catalogue-upload)

## CI

CI runs on every push and pull request to `master` (`.github/workflows/dotnet.yml`). It is a single
`build-test` job on `ubuntu-latest` that pins the SDK from `global.json` and runs, against
`DataCatalogueUpload.slnx`:

```bash
dotnet restore DataCatalogueUpload.slnx
dotnet format DataCatalogueUpload.slnx --verify-no-changes --no-restore   # code style
dotnet build DataCatalogueUpload.slnx --configuration Release --no-restore  # warnings as errors
dotnet test DataCatalogueUpload.slnx --configuration Release --no-build      # all 4 test projects
```

Reproduce CI locally before pushing by running those four commands. If they pass locally, CI should pass.

### Fixing common CI failures

| Failure | Fix |
|---------|-----|
| `dotnet format --verify-no-changes` fails | run `dotnet format DataCatalogueUpload.slnx` to reformat, then commit |
| Build fails (warning-as-error / analyzer) | fix the code; do not loosen `Directory.Build.props` or suppress the analyzer |
| Nullable / unused-using warnings | address them - they are errors here |
| `dotnet test` failures | reproduce with `dotnet test --filter "FullyQualifiedName~Name"`, fix the code or test |
| Package restore / version error | the version belongs in `Directory.Packages.props` (central package management), not on the `PackageReference` |

## Pull requests

Keep PRs focused and small. Use this description template:

```markdown
## Summary
What this PR does, in 1-2 sentences.

## Motivation
Why the change is needed (link issues with `Closes #<n>`).

## Changes
- Bullet the key changes.

```

Open PRs against `master`. Prefer the `gh` CLI: `gh pr create`. **Always assign the PR to `mf-16`** (use
`--assignee mf-16` on `gh pr create`, or `gh pr edit <n> --add-assignee mf-16` afterwards). PRs are **not**
added to the project board.

## Project board

Every **issue** must be on the **BBMRI-IT coordination** project (org `BBMRI-cz`) with **Status = No Status**
(cleared), **Category = Data catalogue**, and assigned to `mf-16`. A project workflow auto-adds new issues to
the board (see below), so the task is to fix the auto-added item's fields, not to add it. PRs are intentionally
excluded from the board (assignee only - see above) to keep the table readable.

Reference IDs (org `BBMRI-cz`, project number `3`):

| Item | ID |
|------|----|
| Project ID | `PVT_kwDOBuSb9M4AbC0J` |
| Status field | `PVTSSF_lADOBuSb9M4AbC0JzgRYUBg` |
| Category field | `PVTSSF_lADOBuSb9M4AbC0Jzg3LQOQ` |
| Category → `Data catalogue` option | `d9fce010` |

Setting fields needs the `project` token scope. If a command fails with `your authentication token is missing
required scopes [read:project]`, run once:

```bash
gh auth refresh -s read:project,project --hostname github.com
```

### Adding an issue to the board

A project **auto-add workflow already puts every new issue on the board** (usually with a wrong/blank Category
and Status = `Todo`). So do **not** run `gh project item-add` — it creates a *duplicate* board item. Instead,
after creating the issue (always with `--assignee mf-16`), find the item the workflow added and fix its fields:

```bash
# 1. Find the board item id for the issue (item-list defaults to 30 rows - always pass --limit).
#    The auto-add can lag a few seconds after issue creation.
ITEM_ID=$(gh project item-list 3 --owner BBMRI-cz --limit 500 --format json \
  -q ".items[] | select(.content.number==<issue-number>) | .id")

# 2. Category = Data catalogue
gh project item-edit --project-id PVT_kwDOBuSb9M4AbC0J --id "$ITEM_ID" \
  --field-id PVTSSF_lADOBuSb9M4AbC0Jzg3LQOQ --single-select-option-id d9fce010

# 3. Status = No Status (auto-added items default to Todo, so clear it)
gh project item-edit --project-id PVT_kwDOBuSb9M4AbC0J --id "$ITEM_ID" \
  --field-id PVTSSF_lADOBuSb9M4AbC0JzgRYUBg --clear
```

If you already ran `item-add` and created duplicates, delete the extra item(s) with
`gh project item-delete 3 --owner BBMRI-cz --id <item-id>` so exactly one remains per issue.

## Issues

When filing an issue, assign it to `mf-16` and add it to the project board with Status = No Status and
Category = Data catalogue (see the **Project board** section above for the exact commands):

```bash
gh issue create --repo BBMRI-cz/data-catalogue-upload \
  --title "..." --body "..." --assignee mf-16
```

### Bug report

```markdown
## Description
What is wrong, in 1-2 sentences.

## Steps to reproduce
1. ...
2. ...

## Expected behavior
What you expected to happen.

## Actual behavior
What actually happened (include error output / run summary JSON if relevant).

## Environment
.NET SDK version (`dotnet --version`), OS, relevant env vars / config.
```

### Feature request

```markdown
## Motivation
The problem or need this addresses.

## Proposed solution
What you want to happen.

## Acceptance criteria
- [ ] Observable condition 1
- [ ] Observable condition 2
```

Label issues appropriately (e.g. `bug`, `enhancement`, `chore`) when the labels exist.
