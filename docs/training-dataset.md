# Training dataset

Both source APIs - biobank and sequencing - running on your own computer over a synthetic dataset,
for work that needs data shaped like ours without access to our servers (#112). **No real patient is
in it.** Every value is drawn from how often it occurs in the real databases, and every identifier and
date is generated.

You read it the way the uploader does: `GET /patients` from the biobank API, then
`GET /sequencing?predictive_number=` on the sequencing API for each predictive number the patients'
samples carry.

## What you need

- **Docker** with Compose: Docker Desktop on Windows or macOS, Docker Engine with the compose plugin
  on Linux. A couple of GB of free memory for the build; the running stack uses under 1 GB.
- **git**.
- Ports **8001** and **8002** free. Nothing else: the .NET SDK runs inside the build.

## Start it

```bash
git clone https://github.com/BBMRI-cz/data-catalogue-upload.git
cd data-catalogue-upload
docker compose -f compose.training.yml up --build
```

The first build downloads the .NET and PostgreSQL images and takes a few minutes. Then:

1. Two PostgreSQL databases start, empty. They live in memory and publish no port.
2. `training-seeder` creates both schemas, writes the dataset and exits. It logs
   `Seeded 40617 patients, ... in N s` - anything from half a minute to a couple of minutes,
   depending on the machine and what else it is doing.
3. The two APIs start on `http://localhost:8001` and `http://localhost:8002`.

It is ready when both answer:

```bash
curl http://localhost:8001/health      # {"status":"ok"}
curl http://localhost:8002/health
```

Add `-d` to run it in the background. `Ctrl+C`, or `docker compose -f compose.training.yml down`,
stops it. The data goes with it, but nothing is lost: every start generates the same dataset again.

| Setting | Effect |
|---------|--------|
| `SEED_PATIENTS=500` | A smaller dataset, quicker to start and to read. Unset: as many patients as the real database has (40,617). |
| `SEED=7` | A different dataset of the same shape. Unset: the default, the same for everyone. |

```bash
SEED_PATIENTS=500 docker compose -f compose.training.yml up --build        # Linux, macOS
$env:SEED_PATIENTS=500; docker compose -f compose.training.yml up --build  # Windows PowerShell
```

A new value takes effect on a fresh start: run `down` first. On a stack that is still running, the
seeder sees data and leaves it as it is.

## Read it

| Request | Answers |
|---------|---------|
| `GET http://localhost:8001/patients` | Every patient (about 30 MB of JSON): consent, sex, birth year and month, radiology accession numbers, `samples` (tissue, serum, genome) and `diagnostic_specimens`. |
| `GET http://localhost:8002/sequencing?predictive_number=2019/5342` | The sequencing for one predictive number: `samples`, each with its `runs`, and each run with its library preparation (kits, panel and its genes), analyses (pipeline, reference genome, quality) and files. |
| `GET http://localhost:8002/summary` | Totals over the sequencing data. |

Values come as the sources write them: diagnoses as ICD-10 codes (`C504`), morphology as ICD-O-3
(`8500/3`), staging as the biobank's pTNM text, material types as the biobank's codes with an English
`material_type_label`, kits, panels and instruments by the vendor's name.

A predictive number from `/patients` always finds sequencing data. The sequencing side writes it the
way the sources do, so the same number can come back as `2020/1052`, `2020_1052_DNA` or `1052-20`;
the API matches all three. A number it does not know answers `200` with an empty `samples` list.

To walk the whole dataset, in Python:

```python
import json
import urllib.parse
import urllib.request

patients = json.load(urllib.request.urlopen("http://localhost:8001/patients"))
numbers = {s["predictive_number"] for p in patients for s in p["samples"] if s["predictive_number"]}

for number in sorted(numbers):
    url = "http://localhost:8002/sequencing?predictive_number=" + urllib.parse.quote(number)
    sequencing = json.load(urllib.request.urlopen(url))
    for sample in sequencing["samples"]:
        for run in sample["runs"]:
            library = run["library_preparation"] or {}
            print(number, run["instrument_model"], library.get("library_prep_kit"))
```

## What is real and what is not

Only counts left the servers - how many rows hold each value - never a row. The seeder draws from
those counts with a fixed random seed.

| | Fields |
|-|--------|
| **Every value, as often as in the real data** | Diagnosis, morphology, pTNM, material type, sex, consent, `retrieved`. Library preparation and enrichment kits, panel, pipeline, reference genome, platform, instrument, chemistry, reagent kit, read structure, `source_class`. |
| **Real, whole** | The panel table with its gene lists. Run configurations - which instrument, kits and settings go together - and which library preparations run on which configuration. These are the lab's equipment and vendor data, not patients'. |
| **Spread like the real data** (min, median, max) | Read depth, observed read length, Q30, cluster density and yield, input amount, aliquot counts. |
| **Shaped like the real data** | How many samples and specimens a patient has, how many samples a run has, how often a field is filled in. |
| **Generated** | Every identifier (patient, sample, biopsy, predictive and accession numbers, run and flowcell ids, sample folder names), every date, birth year and month, experiment names, file paths and sizes. |

What this means when you read it:

- **Values are drawn per field.** A sample's diagnosis and its morphology, or a patient's diagnosis
  and the panel their tumour was sequenced on, are not necessarily a plausible pair: only per-field
  counts left the server, so no real patient's combination can be rebuilt. A surgery's samples do
  share one diagnosis, morphology and staging, and a run's kits and instrument belong together.
- **Rare codes may be missing.** A code that occurs once in 70,000 real specimens is unlikely to be
  drawn. The full list of every code with its real count is in
  [`tools/TrainingSeeder/biobank-vocabulary.json`](../tools/TrainingSeeder/biobank-vocabulary.json)
  and [`sequencing-vocabulary.json`](../tools/TrainingSeeder/sequencing-vocabulary.json).
- **The awkward cases are in it on purpose**: patients without consent (they carry no samples),
  patients with specimens but no samples, one surgery yielding several samples that share a predictive
  number, MammaPrint runs (which the uploader leaves out), a sample sequenced on more than one run, and
  the three predictive-number forms. Consent and MammaPrint follow the real rates; resequencing (one
  sample in 20) and the two other forms (one case in ten each) are fixed shares.

## How it is built

- [`compose.training.yml`](../compose.training.yml) is separate from the three production stacks.
  Seeding is not a flag on those, so no wrong setting on a server can put generated patients into a
  real database. Its containers are named `training-*`; it has no uploader.
- [`tools/TrainingSeeder`](../tools/TrainingSeeder) builds every record through the services' domain
  factories and saves it through their own repositories, so a schema change in this repository
  reaches the dataset without anyone editing the seeder. It only ever fills **empty** databases and
  only migrates a database that has no schema yet; anything else it leaves alone. No service
  references it and no production image contains it.
- `TrainingSeeder.IntegrationTests` runs it against SQLite in CI: a domain change that breaks it, or a
  model change without its migration, fails the build.
- The APIs run with `RUN_MIGRATIONS=false` and `DisableScheduler=true`: the seeder made the schema, and
  there is no export to ingest. `POST /admin/ingest` there fails for want of one and changes nothing.
- Ports are the production ones, on loopback. For an end-to-end test, run the uploader on the host
  ([`DEVELOPMENT.md`](../DEVELOPMENT.md)) with `BIOBANK_API_URL=http://localhost:8001` and
  `SEQUENCING_API_URL=http://localhost:8002`. The uploader's container cannot reach this stack: on
  Linux, `host.docker.internal` does not reach a port published on loopback only. Mind that the
  uploader writes to whichever catalogue `.env` names.

### Refreshing the vocabulary

[`tools/TrainingSeeder/export-vocabulary.sql`](../tools/TrainingSeeder/export-vocabulary.sql) reads
one source database in a read-only transaction and prints the counts as JSON; its header has the
commands. It never exports an identifier, date, path or free-text value - but the repository is
public, so **read both files before committing them**. The seeder embeds them at build time.

## Troubleshooting

| Symptom | Cause |
|---------|-------|
| `port is already allocated` | Something already listens on 8001 or 8002 - another copy of this stack, or the production APIs. Stop it. |
| The seeder logs `Left alone: the databases already hold ...` | The stack was already running. `docker compose -f compose.training.yml down`, then `up` again. |
| An API exits at once with `dependency failed to start` | The seeder failed. `docker compose -f compose.training.yml logs training-seeder` says why. |
| `Cannot load library libgssapi_krb5.so.2` in a log | Harmless: the database client looks for Kerberos, which the .NET images do not ship, and carries on without it. |
