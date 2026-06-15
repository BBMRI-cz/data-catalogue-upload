---
name: github-workflow
description: GitHub CI, pull request, and issue conventions for the data-catalogue-upload repository. Use when pushing to GitHub, opening or describing a pull request, filing an issue, or investigating a failing CI run. Covers what CI checks run, reproducing them locally, fixing common failures, and PR/issue templates.
---

# GitHub workflow (data-catalogue-upload)

## CI

CI runs on every push and pull request to `master` (`.github/workflows/ci.yml`). On Python 3.11 with uv it runs, in order:

```bash
uv run ruff check .
uv run ruff format --check .
uv run mypy .
uv run pytest
```

Reproduce all of CI locally before pushing by running the same four commands. If they pass locally, CI should pass.

### Fixing common CI failures

| Failure | Fix |
|---------|-----|
| `ruff check` errors | `uv run ruff check --fix .`, then review remaining manual fixes |
| `ruff format --check` fails | `uv run ruff format .` to reformat |
| `mypy` missing stubs | add a typed stub package via `uv add --group dev types-<pkg>` (e.g. `types-requests` is already present) |
| `mypy` type errors | fix the code; do not loosen `[tool.mypy]` config |
| `pytest` failures | reproduce with `uv run pytest -v`, fix the code or test |

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

Open PRs against `master`. Prefer the `gh` CLI: `gh pr create`.

## Issues

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
Python version, OS, relevant env vars / config.
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
