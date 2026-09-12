---
name: docs-maintenance
description: Audit and update the repository's documentation and Claude Code skills so they stay accurate against the code. Use after an architectural or tooling change (renamed solution/projects, added/removed packages or services, changed env vars, compose files or deployment topology, CI, migrations, or layer conventions), or when asked to "update the docs/skills", "check the docs are current", or "the README is out of date". Covers the four root docs (README, DEVELOPMENT, ARCHITECTURE, AGENTS/CLAUDE), docs/*.md, and .claude/skills/*.
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
| `docs/deployment.md` | the compose files, the services in each, the env keys they substitute, and the ports |
| `docs/catalogue-api-contract.md` | the live EMX2 schema and `Emx2*` classes in `Uploader.Infrastructure/Http/`. **Never name a real token, its claims, or its scope here** - this file ships to production; state the requirement, not the disclosure |
| `docs/pseudonymization.md` | `CatalogueMapper` + `BiobankMapping`, one row per published catalogue column |
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
   `dotnet-tools.json`, and a doc must not describe a package the code doesn't reference. (FluentValidation
   is referenced now - it backs the application-level `ValidationBehavior` - so it is no longer a dead claim.)
6. **Type & service names.** Domain services, aggregates, ports, and helpers named in docs/skills must exist
   (`FingerprintSyncPlanner`, `Fingerprint.Of`/`ComputeFingerprint()`, the uploader's per-source mappers
   in `Mapping/` (there is no single `SourceMapper` any more), `XmlValueReader`,
   the `I*Gateway`/`I*Repository` ports). The biobank has no domain "cleaning service"; the uploader has no
   "FingerprintCalculator" - don't reintroduce names that aren't in the code.
7. **No Python residue.** No `pytest`, `mypy`, `ruff`, `uv`, `dataclass`, `Protocol`, `apps/`, `alembic`,
   `conftest`, or `.python-version` anywhere in docs or skills.
8. **Compose topology.** Three files, one stack each: `compose.biobank.yml` (biobank-db, biobank-api),
   `compose.sequencing.yml` (sequencing-db, sequencing-api), `compose.uploader.yml` (uploader-db, uploader).
   There is no `compose.prod.yml` - it was removed when the stacks were split, so any doc still naming it is
   stale. Every service a doc lists must appear in the file it names, and every `${VAR}` those files
   substitute must appear in `.env.example`.
9. **Secrets.** No token, key, or credential value in any tracked file, and no description of a real token's
   claims or privileges either. `.env` must not be tracked; `.env.example` carries key names with empty
   values for anything secret.

## How to do it

```bash
# stale solution name or Python tooling anywhere it shouldn't be
rg -n "DataCatalogue\.slnx|pytest|mypy| ruff| uv |dataclass|Protocol|apps/|alembic" \
   README.md DEVELOPMENT.md ARCHITECTURE.md AGENTS.md .claude/skills

# a documented type that no longer exists in code
rg -n "FingerprintCalculator|IBiobankCleaningService" src docs *.md .claude/skills

# a compose file that no longer exists
rg -n "compose\.prod\.yml" . --glob '!.git'

# every variable the compose files substitute is documented in .env.example
for v in $(rg -o '\$\{[A-Z_]+' compose.*.yml | cut -d'{' -f2 | sort -u); do
  rg -q "^$v=" .env.example || echo "missing from .env.example: $v"
done

# a leaked credential, in the working tree or anywhere in history
rg -n "eyJ[A-Za-z0-9_-]{20,}" . --glob '!.git'
git log -p --all | rg -n "eyJ[A-Za-z0-9_-]{20,}"
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
