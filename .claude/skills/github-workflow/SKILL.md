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

Every **issue** must be added to the **BBMRI-IT coordination** project (org `BBMRI-cz`) with **Status = No
Status** (cleared - the default for newly added items), **Category = Data catalogue**, and assigned to
`mf-16`. PRs are intentionally excluded from the board (assignee only - see above) to keep the table readable.

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

After creating the issue (always with `--assignee mf-16`), add it to the board, leave Status as **No Status**,
and set Category:

```bash
# 1. Add the issue to the project; capture the returned item id
ITEM_ID=$(gh project item-add 3 --owner BBMRI-cz --url <issue-url> --format json -q .id)

# 2. Status = No Status. Newly added items default to No Status, so nothing to
#    set. If an item already has a status, clear it back to No Status with:
#    gh project item-edit --project-id PVT_kwDOBuSb9M4AbC0J --id "$ITEM_ID" \
#      --field-id PVTSSF_lADOBuSb9M4AbC0JzgRYUBg --clear

# 3. Category = Data catalogue
gh project item-edit \
  --project-id PVT_kwDOBuSb9M4AbC0J \
  --id "$ITEM_ID" \
  --field-id PVTSSF_lADOBuSb9M4AbC0Jzg3LQOQ \
  --single-select-option-id d9fce010
```

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
