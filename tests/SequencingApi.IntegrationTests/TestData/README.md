# MMCI ingestion fixtures

A miniature of MMCI's source layout, used by `MmciSequencingDataSourceTests` and
`IngestEndToEndTests`. Copied next to the test assembly by the csproj `Content` glob.

## Why the identifiers are shortened

Real sample folders are named `mmci_predictive_<uuid>` and run folders carry the full flowcell id.
Those names put the deepest file here —
`.../Samples/<sample>/Analysis/Reports/<sample>_Coverage_Curve_Report1_Statistics.txt` — past
Windows' 260-character path limit once the build copies the tree under `bin/Release/net10.0/`. The
ids are therefore abbreviated (`p0001`, run id without the leading flowcell zeros). Nothing reads
meaning from them: the domain treats a sample id as opaque, which is exactly what makes this safe.
**Structure, file names and file contents are otherwise faithful**, including the Windows-1250
encoding, decimal commas and mixed line endings.

The `.fastq.gz`, `.bam` and `.pdf` files are small text placeholders. The reader only stats them for
size; it never opens them.

## What each part exercises

| Path | The case it covers |
|---|---|
| `Runs/2024/MiSEQ/complete-runs/240104_M02340_0399_LCBRW` | The complete case: paired-end, one lane, full `Analysis/` with NextGENe reports. |
| ” `Samples/p0001` | Reads plus analysis, quality metrics with decimal commas, and a `_Parameters.txt` that resolves the panel. |
| ” `Samples/p0002/FASTQ` (empty) | A sample folder that exists with no reads at all — over a hundred real ones do. |
| ” `Samples/p0003` (empty) | An orphan folder absent from the sample sheet: reported, not ingested. |
| ” `SampleSheet.csv` | Lists `p0001` **twice**: the first row wins and the repeat is reported. Indexing the rows with `ToDictionary` threw on this, which ended the entire ingest rather than that one run. |
| `.../complete-runs/240430_M02340_0412_ABCDE` | Single-read run (R1 only, no R2) — the MammaPrint case; also re-sequences `p0001`. |
| ” `Samples/p0050/FASTQ` | Carries an R2 in a single-read run: **more** read files than the read structure implies, which is reported. |
| `.../mamma-print/240430_M02340_0412_ABCDE` | **The same run id in a second subtype folder**, with fewer samples: de-duplicated away and reported. |
| `Runs/2024/NextSeq/240102_NB552710_0064_AHG7L` | NextSeq: 4 lanes (8 read files/sample), `Sample_Type` DNA/RNA, no analysis. Re-sequences `p0001` a third time. |
| ” `Samples/p0009/FASTQ` | Seven files, one lane's R2 missing: **fewer** than the read structure implies, also reported. |
| `Runs/backups`, `Runs/errors`, `Runs/logs` | Top-level folders that must be excluded from the walk. |
| `Libraries/LibrariesV240101.csv` | The older table version — the only one carrying input amount / insert size / read length. |
| `Libraries/LibrariesV250101.csv` | The newest by the version in its name: authoritative, but dropped those three columns. |
| `MappingTable/predictive.json` | Pseudonymized → real predictive number. Covers `p0001`/`p0009`/`p0050` but **not** `p0002`. |
| `MappingTable/patient.json`, `samples.json` | Present so it is visible that this service never opens them. |

`p0001` appears in three runs, which makes it the re-sequencing case: one sample aggregate holding
three run-samples.
