---
name: docs-maintenance
description: Audit and update the repository's documentation and Claude Code skills so they stay accurate against the code. Use after an architectural or tooling change (renamed solution/projects, added/removed packages or services, changed env vars, CI, migrations, or layer conventions), or when asked to "update the docs/skills", "check the docs are current", or "the README is out of date". Covers the four root docs (README, DEVELOPMENT, ARCHITECTURE, AGENTS/CLAUDE) and .claude/skills/*.
---

# Docs & skills maintenance (data-catalogue-upload)

Documentation drifts silently - it never fails a build. This skill is a checklist for keeping the prose true
to the code. Verify every claim against a source file before trusting it; if a doc names a type, file, flag,
or command, **open the real thing and confirm it still exists** with that name.

## What this governs

| Artifact | Source of truth it must match |
|----------|-------------------------------|
| `README.md` | the solution name, services, and the high-level stack |
| `DEVELOPMENT.md` | prerequisites, env vars, EF migration commands, run/test/CI commands |
| `ARCHITECTURE.md` | the layer diagram, domain services, and the sync flow |
| `AGENTS.md` | conventions + commands. **`CLAUDE.md` only contains the text `AGENTS.md`** and mirrors it - edit `AGENTS.md`, never duplicate content into `CLAUDE.md`. |
| `.claude/skills/*/SKILL.md` | each skill's frontmatter `description` and its body |

> The available-skills list the harness shows is built from each skill's frontmatter `description:`. That
> field is what future sessions match on, so keep it accurate and specific - not just the body.

## Audit checklist

Cross-check each item against the live code/config:

1. **Solution & project names.** `DataCatalogueUpload.slnx` and the `src/<Service>/<Service>.{Domain,
   Application,Infrastructure,Web|Host}` / `tests/*` layout. Grep for any old name (e.g. `DataCatalogue.slnx`)
   anywhere, including CI and docs.
2. **Env vars.** The lists in `DEVELOPMENT.md`/`AGENTS.md` must match the keys read in
   `BiobankOptions.FromConfiguration` and `UploaderOptions.FromConfiguration`
   (`src/*/*.Infrastructure/Configuration/*Options.cs`).
3. **CI commands.** Must match `.github/workflows/dotnet.yml` verbatim (restore / format --verify-no-changes /
   build Release / test) and the solution name there.
4. **EF migration commands.** Project/startup-project paths in the docs must match the real
   Infrastructure/Host projects; `dotnet-ef` is the local tool in `dotnet-tools.json`.
5. **Package/tooling claims.** Every package or tool a doc names must exist in `Directory.Packages.props` /
   `dotnet-tools.json`. **Watch for dead claims** - e.g. FluentValidation was once documented but used
   nowhere; a doc must not describe a package the code doesn't reference.
6. **Type & service names.** Domain services, aggregates, ports, and helpers named in docs/skills must exist
   (`FingerprintSyncPlanner`, `Fingerprint.Of`/`ComputeFingerprint()`, `SourceMapper`, `XmlValueReader`,
   the `I*Gateway`/`I*Repository` ports). The biobank has no domain "cleaning service"; the uploader has no
   "FingerprintCalculator" - don't reintroduce names that aren't in the code.
7. **No Python residue.** No `pytest`, `mypy`, `ruff`, `uv`, `dataclass`, `Protocol`, `apps/`, `alembic`,
   `conftest`, or `.python-version` anywhere in docs or skills.

## How to do it

```bash
# stale solution name or Python tooling anywhere it shouldn't be
rg -n "DataCatalogue\.slnx|pytest|mypy| ruff| uv |dataclass|Protocol|apps/|alembic" \
   README.md DEVELOPMENT.md ARCHITECTURE.md AGENTS.md .claude/skills

# a documented type that no longer exists in code
rg -n "FingerprintCalculator|IBiobankCleaningService|FluentValidation" src docs *.md .claude/skills
```

For each hit, open the named source file, decide what the code actually does now, and edit the doc/skill in
place. Keep links relative and clickable (`[file](path)` / `[file:42](path#L42)`). Prefer correcting a
sentence over deleting it - the goal is an accurate doc, not a shorter one.

## After updating

- Re-run the greps above and expect no stale hits.
- Skim each edited doc so it still reads coherently (a rename can leave a dangling clause).
- If you changed a skill's frontmatter `description`, note that the harness picks it up on the next session.
- These are docs, so there's nothing to build - but if you also touched config (e.g. removed a package pin),
  run the standard `dotnet format` / `dotnet build -c Release` / `dotnet test` to confirm nothing broke.
