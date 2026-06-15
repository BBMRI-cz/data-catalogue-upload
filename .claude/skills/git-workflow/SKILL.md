---
name: git-workflow
description: Git commit and branch conventions for the data-catalogue-upload repository. Use when staging changes, writing commit messages, or creating branches. Covers Conventional Commits, branch naming, and the pre-commit quality checklist.
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

Useful scopes for this repo: `sync`, `domain`, `builders`, `infra`, `db`, `migrations`, `ci`.

Examples:

```
feat(builders): add radiology imaging-study builder
fix(sync): count per-entity failures without aborting the run
chore: prepare repo for agentic development
refactor(domain): extract fingerprint helper into sync module
test(builders): cover clinical builder missing-field handling
docs: document required API URL env vars
```

Keep the summary in the imperative mood and under ~72 characters. Add a body (after a blank line) to explain the "why" when it is not obvious.

## Branch naming

`type/short-kebab-description`, matching the commit types:

```
feat/radiology-builder
fix/sync-failure-count
chore/agentic-dev-docs
```

## Before committing

Run the same checks CI runs, for each package you touched (`<pkg>` = `uploader` or `biobank_api`), and make sure they pass:

```bash
uv run ruff check apps/<pkg>
uv run ruff format --check apps/<pkg>
uv run mypy apps/<pkg>
uv run pytest apps/<pkg>/tests
uv lock --check
```

`uv run ruff format apps/<pkg>` (without `--check`) auto-fixes formatting. Do not commit secrets or a real `.env` - only each member's `.env.example` is tracked. Do not hand-edit `uv.lock`.
