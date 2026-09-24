# Deploying and running the uploader

A runbook for deploying this repository onto the machines that hold the source data, and running one
sync into the MOLGENIS EMX2 data catalogue. Follow it top to bottom the first time.

## 1. Three stacks

There is one compose file per stack, and each is deployable on its own machine.

| stack | compose file | services | needs |
|---|---|---|---|
| biobank | `compose.biobank.yml` | `biobank-db`, `biobank-api` | `/home/mou/patient_data` |
| sequencing | `compose.sequencing.yml` | `sequencing-db`, `sequencing-api` | `/muni-sc/OrganisedRuns`, `/muni-sc/Libraries`, `/home/export/pseudonymization_table` |
| uploader | `compose.uploader.yml` | `uploader-db`, `uploader` | nothing on disk |

The two source stacks are tied to a machine because of what they read. A Docker bind mount resolves
on the host running the container and nowhere else, and pointing one at a path the host does not have
gets you an empty directory rather than an error, so the service starts, reads nothing and reports
zero records. Each therefore runs where its data already is.

The uploader reads no files. It talks to both source APIs over HTTP, so it can sit on either source
machine or on a third.

Docker Compose only talks to its local Docker daemon. It cannot start a container on another machine,
so there is no single file orchestrating all of this. Check the repository out on each machine and
run the stacks it owns.

**All three on one machine works too.** Run all three compose files there. They stay three separate
Compose projects on three separate networks, which matters for one thing only: container names do not
resolve between them, so the uploader addresses the source APIs as `host.docker.internal` rather than
`biobank-api`. `.env.example` spells out both forms.

## 2. Before you start

On each machine:

- Docker Engine with the Compose plugin (`docker compose version`)
- Git, and read access to this repository
- The mount paths for the stacks that machine runs, readable by the Docker daemon

Between machines, if they are split:

- The uploader's machine must reach the biobank on TCP 8001 and sequencing on TCP 8002
- Ideally over a private network or VPN, for the reason in section 6

For the catalogue:

- An account on `https://data.bbmri.cz` that can write to the `FairGenomesTest` schema

Every machine gets the same checkout and the same `.env`, filled in for what it runs:

```bash
git clone git@github.com:BBMRI-cz/data-catalogue-upload.git
cd data-catalogue-upload
git checkout feat/emx2-catalogue-upload
cp .env.example .env
```

## 3. The biobank stack

In `.env`, set the biobank section. `BIOBANK_BIND` is the address the uploader will reach this API
on: loopback if the uploader runs on this machine, a private address if it does not.

```
BIOBANK_BIND=127.0.0.1
BIOBANK_XML_EXPORT_DIR=/home/mou/patient_data
```

Start it and load the export:

```bash
docker compose -f compose.biobank.yml up -d --build
curl -s http://127.0.0.1:8001/health
curl -X POST http://127.0.0.1:8001/admin/ingest
```

The ingest reports how many patients it parsed and stored.

**Never prune the export directory.** Each weekly file lists only the last ~60 days of a patient's
samples, so the API merges every file a patient ever had
([`docs/patient-data-report.md`](patient-data-report.md#how-a-patients-files-relate)). Deleting old
files to free space silently deletes their samples on the next ingest.

Confirm real data is being served:

```bash
curl -s http://127.0.0.1:8001/patients | head -c 400
```

An empty list means the bind mount is wrong — see section 10.

Ingestion also runs weekly on its own schedule. The manual POST is for the first load and for
re-reading after the export changes.

## 4. The sequencing stack

In `.env`, set the sequencing section. Same rule for `SEQUENCING_BIND` as for the biobank.

```
SEQUENCING_BIND=127.0.0.1
SEQUENCING_RUNS_DIR=/muni-sc/OrganisedRuns
SEQUENCING_LIBRARIES_DIR=/muni-sc/Libraries
SEQUENCING_MAPPING_DIR=/home/export/pseudonymization_table
```

```bash
docker compose -f compose.sequencing.yml up -d --build
curl -s http://127.0.0.1:8002/health
curl -X POST http://127.0.0.1:8002/admin/ingest
```

The sequencing ingest walks the whole run tree and takes a while on the first pass. If it starts and
never finishes, see section 10.

## 5. The uploader stack

Put this wherever you like. If the machines are split, the sequencing machine is the better host: the
uploader makes one request per sequenced sample, so that traffic is many small round trips, while the
biobank is a single large response that costs bandwidth once rather than latency repeatedly.

In `.env`, point it at both source APIs. Both are required — an empty value means "not deployed", so
a blank would upload nobody without complaining, and Compose refuses to start rather than allow it.

```
# both source stacks on this same machine
BIOBANK_API_URL=http://host.docker.internal:8001
SEQUENCING_API_URL=http://host.docker.internal:8002

# or, biobank on another machine
BIOBANK_API_URL=http://10.0.0.11:8001
```

Fill in the catalogue section too — section 7 covers the token. Then start the database and check the
uploader can see both sources before running anything:

```bash
docker compose -f compose.uploader.yml up -d uploader-db
curl -s http://10.0.0.11:8001/patients | head -c 200
```

If that hangs or is refused, the firewall or the bind address is wrong. Fix it before going on: the
uploader treats an unreachable biobank as fatal, since every patient comes from it.

## 6. The network between machines

Neither source API has any authentication. `/patients` is an anonymous GET that returns real patient
identifiers, birth years, sex and ICD-10 diagnoses, before any pseudonymization, and `/admin/ingest`
is an anonymous POST that anyone able to reach it can fire.

On a single locked-down host that is containable. Across a network it is not. At minimum:

- the bind variables name a private or VPN address, never `0.0.0.0` and never a public one
- a firewall rule allows the port **only** from the uploader's machine:

```bash
sudo ufw allow from 10.0.0.12 to any port 8001 proto tcp
sudo ufw deny 8001
```

Better, if you have it: put the machines on a WireGuard interface and bind to that address. Adding
authentication to the source APIs is tracked separately and is not part of this deployment.

## 7. A catalogue token

Sign in at `https://data.bbmri.cz`, open the account menu, choose **Manage tokens**, and create one
scoped to **Editor on `FairGenomesTest`**. Do not use an admin token: it reaches every schema on the
host, which is far more than a scheduled job should hold.

Put it in `.env` on the uploader's machine:

```
CATALOGUE_API_URL=https://data.bbmri.cz
CATALOGUE_SCHEMA=FairGenomesTest
CATALOGUE_TOKEN=<the token>
CATALOGUE_STUDY_ID=mmci_biobank
CATALOGUE_STUDY_NAME=MMCI Biobank
```

Check it before running anything:

```bash
curl -s -X POST https://data.bbmri.cz/FairGenomesTest/api/graphql \
  -H "x-molgenis-token: $CATALOGUE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query":"{_session{email roles}}"}'
```

Your email and roles come back if the token is good. `Cannot parse token` means it is not — see
section 10.

`.env` is git-ignored. The token belongs there and nowhere else.

## 8. The first run

```bash
docker compose -f compose.uploader.yml run --rm uploader
```

The uploader applies its own database migrations, runs one sync, prints a JSON summary and exits. It
is a job, not a server, so `run --rm` rather than `up`. Exit code is `0` when nothing failed and `1`
otherwise.

```json
{
  "run_id": "…",
  "scanned": 812,
  "changed": 812,
  "uploaded": 812,
  "deleted": 0,
  "skipped": 0,
  "failed": 0,
  "source_unavailable": 0
}
```

| counter | meaning |
|---|---|
| `scanned` | patients read from the biobank |
| `changed` | aggregates whose fingerprint differed from the last run |
| `uploaded` | aggregates written to the catalogue |
| `deleted` | aggregates removed from the catalogue |
| `skipped` | aggregates unchanged since the last run, or not eligible to publish |
| `failed` | a payload the catalogue rejected, or a source that answered with something unreadable |
| `source_unavailable` | a configured source that could not be reached at all |

`failed` and `source_unavailable` are deliberately separate: the first means fix a payload, the
second means chase an outage. A source with no URL is not deployed, is never contacted, and is
counted in neither — radiology and WSI are blank by design, because no such service exists yet.

An aggregate is a patient, a sample or a sequencing run, so the counters exceed the patient count.

## 9. Checking the catalogue, and running it again

Open `https://data.bbmri.cz/FairGenomesTest/` and look at the tables the uploader writes: `Study`,
`Personal`, `IndividualConsent`, `Clinical`, `Material`, `Biospecimens`, `SamplePreparation`,
`Sequencing`, `Analysis`.

Check three things:

1. **Rows appeared** in each table that had source data behind it.
2. **References resolve.** A `Material` shows its `collectedFromPerson` as a real `Personal` row, not
   a broken key.
3. **No real identifier is present.** Every identifier starts with `mmci_`. A bare biobank id such as
   `271801` or `BBMs:2022:3249:SD` is a leak: stop and report it.

`Clinical.diagnosis` is expected to be empty. The catalogue's `Diagnosis` ontology holds Orphanet
rare-disease terms while the biobank serves ICD-10 oncology codes, and no usable crosswalk exists.
See [`catalogue-api-contract.md`](catalogue-api-contract.md).

Now run it a second time with nothing changed:

```bash
docker compose -f compose.uploader.yml run --rm uploader
```

Everything should land on `skipped`, with `uploaded` at zero. That is the fingerprint comparison
working: the catalogue is written only when the source actually changed.

To leave the test schema as you found it, delete the rows through the EMX2 UI, child tables first —
`Analysis`, `Sequencing`, `SamplePreparation`, `Biospecimens`, `Material`, `Clinical`,
`IndividualConsent`, `Personal`, `Study`. The catalogue refuses to delete a row another row still
references, so any other order fails.

If you also want the next run to re-upload everything, clear the uploader's sync state:

```bash
docker compose -f compose.uploader.yml down -v
```

This destroys the `uploader_db_data` volume, which holds the pseudonym map. Every patient is then
re-pseudonymized under a **new** identifier, so do this only against a test schema you have emptied.

## 10. Troubleshooting

**`/patients` returns an empty list, and the ingest reported zero.** The bind mount points at a path
this host does not have, and Docker created an empty directory rather than failing. Check with
`docker compose -f compose.biobank.yml exec biobank-api ls /data/exports`.

**The sequencing ingest starts and never finishes.** The `/muni-sc` trees are NFSv3 and their lock
manager does not answer, so a file open blocks forever with no timeout and no error.
`DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1` is already set in the compose file for exactly this; confirm
it reached the container with
`docker compose -f compose.sequencing.yml exec sequencing-api env | grep DISABLEFILELOCKING`.
Mounting these trees over NFSv4 avoids the problem at its source, since v4 has locking built into the
protocol and no separate lock daemon.

**The uploader cannot reach a source that is running on the same machine.** The three stacks are
three Compose projects on three networks, so `http://biobank-api:8001` does not resolve from the
uploader. Use `http://host.docker.internal:8001`, which the uploader's `extra_hosts` entry provides.

**`Cannot parse token`.** The token's signature no longer verifies. This happens when the EMX2
instance is restarted or rekeyed, and it invalidates every token issued before, regardless of expiry
date. Mint a new one (section 7). A token that has merely expired reports differently.

**`required variable BIOBANK_API_URL is missing a value`.** `.env` on the uploader's machine has no
`BIOBANK_API_URL`, or no `SEQUENCING_API_URL`. This is deliberate: an empty value would mean "source
not deployed" and the run would upload nobody without complaining.

**`source_unavailable` is non-zero.** A configured source could not be reached. The run still
completes and each patient keeps the data from the sources that did answer. Rows a failed source
would have reported are left alone rather than deleted, so a re-run after the outage repairs itself.

**The run fails immediately with an error instead of a summary.** The biobank could not be listed.
Every patient comes from it, so there is no run to have. Check the firewall, the bind address, and
`BIOBANK_API_URL`.

## 11. Known gaps

- Neither source API authenticates callers. Section 6 is the mitigation, not a fix.
- `Clinical.diagnosis` goes out empty until ICD-10 terms are loaded into the catalogue's `Diagnosis`
  ontology, which is the schema owner's decision.
- Radiology and whole-slide imaging have no source service. Their URLs are blank, they are never
  contacted, and nothing of theirs is published.
- The production `FairGenomes` schema is still the older v1.3 model and cannot receive these
  payloads. `FairGenomesTest` is the only valid target today.

## See also

- [`catalogue-api-contract.md`](catalogue-api-contract.md) — what the catalogue accepts and why
- [`pseudonymization.md`](pseudonymization.md) — which identifier every published field carries
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — how the services fit together
- [`../DEVELOPMENT.md`](../DEVELOPMENT.md) — building and testing from source
