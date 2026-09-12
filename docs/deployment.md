# Running the uploader across two servers

A runbook for deploying this repository onto the two machines that hold the source data, and running
one sync into the MOLGENIS EMX2 data catalogue. Follow it top to bottom the first time.

## 1. Why there are two servers

The two source services each read a directory that exists on only one machine:

| service | needs |
|---|---|
| biobank API | `/home/mou/patient_data` — the patient XML export |
| sequencing API | `/muni-sc/OrganisedRuns`, `/muni-sc/Libraries`, `/home/export/pseudonymization_table` |

A Docker bind mount resolves on the machine running the container and nowhere else. Point a compose
file at a path the host does not have and Docker silently creates an empty directory instead of
failing, so the service starts, reads nothing and reports zero records. Each service therefore runs
where its data already is.

Docker Compose only talks to its local Docker daemon. It cannot start a container on another
machine, so there is no single copy of a compose file orchestrating both. You check the repository
out on **both** servers and run one compose file on each.

| server | compose file | services |
|---|---|---|
| biobank | `compose.biobank.yml` | `biobank-db`, `biobank-api` |
| sequencing | `compose.sequencing.yml` | `sequencing-db`, `sequencing-api`, `uploader-db`, `uploader` |

The uploader runs on the sequencing server. It makes one HTTP request per sequenced sample, so that
traffic is many small round trips and wants to stay local, while the biobank is one large response
that costs bandwidth once rather than latency repeatedly.

The two machines meet over HTTP and nowhere else: the uploader calls the biobank API's `/patients`.
`compose.prod.yml` is unchanged and still runs everything on one machine, for local testing.

## 2. Before you start

On both servers:

- Docker Engine with the Compose plugin (`docker compose version`)
- Git, and read access to this repository
- The mount paths from the table above, readable by the Docker daemon

Between them:

- The sequencing server must reach the biobank server on TCP 8001
- Ideally over a private network or VPN, for the reason in section 5

For the catalogue:

- An account on `https://data.bbmri.cz` that can write to the `FairGenomesTest` schema

## 3. Biobank server

```bash
git clone git@github.com:BBMRI-cz/data-catalogue-upload.git
cd data-catalogue-upload
git checkout feat/emx2-catalogue-upload
cp .env.example .env
```

Edit `.env` and set the biobank section. `BIOBANK_BIND` is the address the sequencing server will
reach this API on — a private address, not `0.0.0.0` and not a public one:

```
BIOBANK_BIND=10.0.0.11
BIOBANK_XML_EXPORT_DIR=/home/mou/patient_data
```

Start it and load the export:

```bash
docker compose -f compose.biobank.yml up -d --build
curl -s http://127.0.0.1:8001/health
curl -X POST http://127.0.0.1:8001/admin/ingest
```

The ingest reports how many patients it parsed and stored. Confirm real data is being served:

```bash
curl -s http://127.0.0.1:8001/patients | head -c 400
```

If that returns an empty list, the bind mount is the first thing to check — see section 10.

Ingestion also runs weekly on its own schedule. The manual POST is only for the first load and for
re-reading after the export changes.

## 4. Sequencing server

```bash
git clone git@github.com:BBMRI-cz/data-catalogue-upload.git
cd data-catalogue-upload
git checkout feat/emx2-catalogue-upload
cp .env.example .env
```

Edit `.env`. `BIOBANK_API_URL` points at the other machine and is required — an empty value means
"this source is not deployed", so a typo would produce a quiet run that uploads nobody. Compose
refuses to start without it.

```
BIOBANK_API_URL=http://10.0.0.11:8001
SEQUENCING_RUNS_DIR=/muni-sc/OrganisedRuns
SEQUENCING_LIBRARIES_DIR=/muni-sc/Libraries
SEQUENCING_MAPPING_DIR=/home/export/pseudonymization_table
```

Fill in the catalogue section too — see section 6 for the token.

Start the services, but not the uploader:

```bash
docker compose -f compose.sequencing.yml up -d --build sequencing-db sequencing-api uploader-db
curl -s http://127.0.0.1:8002/health
curl -X POST http://127.0.0.1:8002/admin/ingest
```

The sequencing ingest walks the whole run tree and takes a while on the first pass. Then check the
link to the other machine:

```bash
curl -s http://10.0.0.11:8001/patients | head -c 200
```

If that hangs or is refused, the firewall or `BIOBANK_BIND` is wrong. Fix it before going on — the
uploader treats an unreachable biobank as a fatal error, since every patient comes from it.

## 5. The network link

Neither source API has any authentication. `/patients` is an anonymous GET that returns real patient
identifiers, birth years, sex and ICD-10 diagnoses, before any pseudonymization, and `/admin/ingest`
is an anonymous POST that anyone able to reach it can fire.

On a single locked-down host that is containable. Across a network it is not. At minimum:

- `BIOBANK_BIND` names a private or VPN address, never `0.0.0.0` and never a public one
- a firewall rule allows TCP 8001 **only** from the sequencing server:

```bash
sudo ufw allow from 10.0.0.12 to any port 8001 proto tcp
sudo ufw deny 8001
```

Better, if you have it: put both machines on a WireGuard interface and bind to that address. Adding
authentication to the source APIs is tracked separately and is not part of this deployment.

## 6. A catalogue token

Sign in at `https://data.bbmri.cz`, open the account menu, choose **Manage tokens**, and create one
scoped to **Editor on `FairGenomesTest`**. Do not use an admin token: it reaches every schema on the
host, which is far more than a scheduled job should hold.

Put it in `.env` on the sequencing server:

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

## 7. The first run

```bash
docker compose -f compose.sequencing.yml run --rm uploader
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

## 8. Checking the catalogue

Open `https://data.bbmri.cz/FairGenomesTest/` and look at the tables the uploader writes:

`Study`, `Personal`, `IndividualConsent`, `Clinical`, `Material`, `Biospecimens`,
`SamplePreparation`, `Sequencing`, `Analysis`.

Check three things:

1. **Rows appeared** in each table that had source data behind it.
2. **References resolve.** A `Material` shows its `collectedFromPerson` as a real `Personal` row, not
   a broken key.
3. **No real identifier is present.** Every identifier starts with `mmci_` — `mmci_patient_…`,
   `mmci_sample_…`, `mmci_biospecimen_…`. A bare biobank id such as `271801` or `BBMs:2022:3249:SD`
   is a leak and means stopping and reporting it.

`Clinical.diagnosis` is expected to be empty. The catalogue's `Diagnosis` ontology holds Orphanet
rare-disease terms while the biobank serves ICD-10 oncology codes, and no usable crosswalk exists.
See `docs/catalogue-api-contract.md` for the detail.

## 9. Running it again

Run the uploader a second time with nothing changed:

```bash
docker compose -f compose.sequencing.yml run --rm uploader
```

Everything should land on `skipped`, with `uploaded` at zero. That is the fingerprint comparison
working: the catalogue is only written when the source actually changed.

To leave the test schema as you found it, delete the rows through the EMX2 UI, child tables first —
`Analysis`, `Sequencing`, `SamplePreparation`, `Biospecimens`, `Material`, `Clinical`,
`IndividualConsent`, `Personal`, `Study`. The catalogue refuses to delete a row another row still
references, so any other order fails.

If you also want the next run to re-upload everything, clear the uploader's sync state:

```bash
docker compose -f compose.sequencing.yml down -v
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
it reached the container with `docker compose -f compose.sequencing.yml exec sequencing-api env | grep DISABLEFILELOCKING`.
Mounting these trees over NFSv4 instead avoids the problem at its source, since v4 has locking built
into the protocol and no separate lock daemon.

**`Cannot parse token`.** The token's signature no longer verifies. This happens when the EMX2
instance is restarted or rekeyed, and it invalidates every token issued before, regardless of its
expiry date. Mint a new one (section 6). A token that has merely expired reports differently.

**`required variable BIOBANK_API_URL is missing a value`.** `.env` on the sequencing server has no
`BIOBANK_API_URL`. This is deliberate: an empty value would mean "source not deployed" and the run
would upload nobody without complaining.

**`source_unavailable` is non-zero.** A configured source could not be reached. The run still
completes and each patient keeps the data from the sources that did answer. Rows a failed source
would have reported are left alone rather than deleted, so a re-run after the outage repairs itself.

**The run fails immediately with an error instead of a summary.** The biobank could not be listed.
Every patient comes from it, so there is no run to have. Check the firewall, `BIOBANK_BIND`, and
`BIOBANK_API_URL`.

## 11. Known gaps

- Neither source API authenticates callers. Section 5 is the mitigation, not a fix.
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
- [`../DEVELOPMENT.md`](../DEVELOPMENT.md) — running it all on one machine
