# Sequencing Data Source Report

Reference for building the new **Sequencing API** over `/muni-sc/OrganisedRuns`.

**Scope — sequencing only.** This API serves the *sequencing* half of the pipeline: the runs,
the reads (FASTQ), the analysis (BAM/VCF/variants), and the run/sample QC metrics — everything
produced by the sequencers and the variant caller. **Patient and biobank-sample data**
(diagnosis, morphology, birth, sex, material type, biopsy number) are **out of scope** — they
are served by the existing **Export Patient API**, which the organiser/pseudonymizer used to
call and which the **new uploader will now call directly**. The clinical JSON that happens to
sit in each run folder is a cached copy of that patient-API data and is *not* this API's
responsibility (see the note in §1).

Sections 1–3 describe MMCI's concrete **source** (the folder structure and its files); section 4
proposes a **biobank-agnostic domain model** for which MMCI is just the first source, and section
5 covers quality/statistics.

The lookup key is the **pseudonymized predictive number** (`mmci_predictive_<uuid>`): on disk it
is a folder name (`Samples/mmci_predictive_<uuid>/`) that recurs across one or more run
directories. The API locates every occurrence across the tree and aggregates the sequencing data
for it. In the generic model this is a `Sample` identified by an opaque `external_id` — the
predictive number is MMCI's instance of it.

Everything below was verified against the live tree and the producer source code (July 2026).

---

## Corpus at a glance (snapshot, 2026-07)

Point-in-time counts from the live tree — for scale/sizing, not live figures.

**7 years (2019–2025) · 361 runs · 5,689 sample folders (~16 samples/run).**

| Machine | Runs | By year |
|---|--:|---|
| **MiSeq** (targeted DNA panels) | 282 | 2019: 13 · 2020: 22 · 2021: 30 · 2022: 39 · 2023: 75 · 2024: 98 · 2025: 5 |
| **NextSeq** (TSO500 RNA/DNA) | 79 | 2021: 4 · 2022: 22 · 2023: 34 · 2024: 18 · 2025: 1 |

- **MiSeq runs by subtype:** `complete-runs` **192** · `mamma-print` **88** · `missing-analysis` **2**.
- **Instruments are not singular:** MiSeq `M02340` (257 runs) + `M06090` (25); NextSeq `NB552710`
  (68) plus five others (`NB501229`, `NB551307`, `NB551575`, `NS500448`, `NS500595`). Key on
  `instrument_id`, don't assume one machine.
- **89 runs are single-read (R1 only)** — essentially every **MammaPrint** run (§6.2).
- **NextSeq samples carry a DNA/RNA `Sample_Type`**; a run mixes both (e.g. 8 DNA / 8 RNA) (§6.2).
- **Panel families seen:** HyperCap / HypCap, SeqCap / SeqCapH, Accel, TSO500, MammaPrint (MP),
  EliGene (EG) — across **16+ distinct `Experiment Name` spellings** (§6.5).

**Data-quality tallies (what an ingest run should expect to skip/flag):**

| Anomaly | Count | Where |
|---|--:|---|
| Sample folders with empty/missing FASTQ | **113** | every year (2024 worst: 67) |
| Runs with empty orphan folders (sheet ≠ folders) | **5** | 2024 only |
| Runs present in both `errors/` **and** the tree | **7** | 2024 (100% of `errors/`) |
| Same run id in **two subtype folders** | ≥1 | `240430…` in complete-runs + mamma-print |
| Zero-byte files | 1 | a `catalog_info` JSON |

---

## 1. Directory layout — every folder explained

```
/muni-sc/OrganisedRuns/
├── <YYYY>/                          # run year
│   ├── MiSEQ/
│   │   ├── complete-runs/           # MiSeq subtype
│   │   │   └── <run_id>/            # one sequencing run
│   │   ├── mamma-print/
│   │   └── missing-analysis/
│   └── NextSeq/
│       └── <run_id>/                # NextSeq runs sit directly here (no subtype level)
├── backups/                         # whole SOURCE runs, kept after a successful organise
├── errors/                          # whole SOURCE runs that FAILED to organise
└── logs/                            # organiser run logs
```

### `<YYYY>/` — year bucket
`"20"` + the first two characters of the run name (`240104_M02340_…` → `2024`). Partitioning only.

### `MiSEQ/` vs `NextSeq/` — sequencing machine
The two machines carry fundamentally different sequencing data:
- **MiSeq** — targeted **DNA** panel (KAPA HyperCap). Full downstream analysis: BAM, VCF,
  variant table, coverage/mutation QC.
- **NextSeq** — **TSO500 RNA/DNA** panel. **FASTQ only** in this tree — no per-sample analysis,
  and no clinical JSON. Analysis is done off-instrument elsewhere.

Both *old* and *new* MiSeq land under `MiSEQ/` (they differ only in raw source layout, not in the
organised output).

### MiSeq subtype: `complete-runs/` · `mamma-print/` · `missing-analysis/`
Only MiSeq has this level, and it tells you **what sequencing data is present**:
- **`complete-runs/`** — samples have full `FASTQ/` **and** `Analysis/`. The complete case.
- **`mamma-print/`** — MammaPrint assay (`Experiment Name` starts `MP`); samples are
  **FASTQ-only** (analysis handled by a separate MammaPrint pipeline).
- **`missing-analysis/`** — analysis expected but absent; samples are **FASTQ-only**.

### `<run_id>/` — one sequencing run
Named as the Illumina run folder: `<YYMMDD>_<instrument>_<run#>_<flowcell>`
(e.g. `240104_M02340_0399_000000000-LCBRW`; NextSeq flowcell looks like `AHG7LGBGXV`). Directly
inside a run — these are the **run-level sequencing metadata** files:

| Entry | What it is / sequencing data it holds |
|---|---|
| `RunInfo.xml` | Run id, instrument, date, **read structure** (per-read cycle counts + which reads are indexes), **flowcell layout** (lanes, surfaces, swaths, tiles). |
| `runParameters.xml` / `RunParameters.xml` | Scanner/instrument id, run number, **reagent kit + chemistry version**, experiment name. NextSeq: `ApplicationName`="NextSeq Control Software", `Chemistry`="NextSeq High", `ChemistryVersion`. (lowercase filename = old MiSeq; capitalised = new MiSeq / NextSeq.) |
| `CompletedJobInfo.xml` | *(MiSeq)* On-instrument job: **start/completion times**, workflow type (GenerateFASTQ), chemistry, adapter sequences. |
| `GenerateFASTQRunStatistics.xml` | *(MiSeq)* **Demux / FASTQ-generation run QC** (per-tile/per-sample cluster stats, yield). |
| `RunCompletionStatus.xml` | *(NextSeq)* Run completion status and yield. |
| `AnalysisLog.txt` | *(MiSeq)* Illumina analysis log. |
| `SampleSheet.csv` | **The index of the run.** INI-style CSV: `[Header]` (experiment name, assay, workflow, application, chemistry, adapters), `[Reads]` (read lengths), `[Settings]`, `[Data]`. The `[Data]` `Sample_ID` column **is the list of predictive numbers** in the run, each with its index (i7/i5 barcodes). NextSeq `[Data]` adds `Sample_Type` (**RNA/DNA**) and `Pair_ID`. |
| `Alignment/` | *(MiSeq)* Run-level alignment logs (raw FASTQs stripped). |
| `catalog_info_per_pred_number/` | *(MiSeq)* Cached **patient/sample** JSON — **out of scope**, see note. |
| `Samples/` | Per-predictive-number sequencing data — see below. |
| `.uploaded` | 0-byte marker: the old uploader already consumed this run. |

> **`catalog_info_per_pred_number/` is out of scope for this API.** It holds one
> `mmci_predictive_<uuid>.json` per predictive number containing **patient and biobank-sample**
> data (patient id, birth, sex, biopsy number, material type, diagnosis, morphology, pTNM,
> timestamps). That same data is served authoritatively by the **Export Patient API**, which the
> new uploader calls directly. The Sequencing API does not need to read or expose it. NextSeq
> runs don't even have this folder. (It is documented here only so you recognise it and skip it.)

### `Samples/` — per-predictive-number sequencing data
One subfolder per predictive number; **the folder name is the predictive number**:

```
Samples/
└── mmci_predictive_<uuid>/          # ← the predictive number (this run's sequencing of it)
    ├── FASTQ/                        # always present for a real sample
    │   ├── <pseudo>_S<n>_L00<lane>_R1_001.fastq.gz
    │   └── <pseudo>_S<n>_L00<lane>_R2_001.fastq.gz
    └── Analysis/                     # MiSeq complete-runs ONLY
        ├── <pseudo>.bam , <pseudo>.bam.bai
        ├── <pseudo>_StatInfo.txt
        ├── <pseudo>_Parameters.txt
        ├── <pseudo>_..._converted.fasta
        ├── bamconversion.log
        └── Reports/
            ├── <pseudo>_Mutation_Report1.txt / .vcf / _Filtered.vcf
            ├── <pseudo>_Mutation_Report1_Statistics.txt
            ├── <pseudo>_Coverage_Curve_Report1.txt / _Statistics.txt / _Settings.txt
            ├── <pseudo>_Coverage_Curve_Report_LowCvrgRegion1.dat
            ├── <pseudo>_SummaryReport.pdf
            ├── PostProcessingTables.log / PostProcessingTablesIndex.dat
            └── Settings/*.ini
```

**`FASTQ/`** — raw paired-end reads (the primary sequencing output).
`<pseudo>_S<n>_L00<lane>_R<read>_001.fastq.gz`: `S<n>` = sample number within the run;
`L00#` = lane (**MiSeq = 1 lane** `L001`; **NextSeq = up to 4 lanes** `L001`–`L004`);
`R1`/`R2` = paired reads. MiSeq sample → 2 files (**or 1 for single-end runs**); NextSeq sample →
up to 8. The count is not fixed — derive it from the RunInfo read structure × lanes (see §6.2).

**`Analysis/`** — **NextGENe V2.4.2.2** (SoftGenetics) output, reference **GRCh37**
(`Human_v37p10_dbsnp135`). MiSeq `complete-runs` only.
| File | Format | Sequencing data |
|---|---|---|
| `<pseudo>.bam` / `.bam.bai` | BAM + index | Aligned reads. |
| `<pseudo>_StatInfo.txt` | text (ISO-8859) | NextGENe header + `[Alignment Statistics]`: **Matched / Aligned / Perfect Reads**; reference & sample file paths. |
| `<pseudo>_Parameters.txt` | text | NextGENe parameter dump (alignment thresholds, mutation filters). |
| `<pseudo>_..._converted.fasta` | FASTA | Preprocessed unique reads. |
| `bamconversion.log` | text | BAM conversion log. |

**`Analysis/Reports/`** — the variant calls and QC (the clinically-actionable results):
| File | Format | Sequencing data |
|---|---|---|
| `<pseudo>_Mutation_Report1.txt` | TSV | **Per-variant table** — see §3.3 for columns. |
| `<pseudo>_Mutation_Report1.vcf` / `_Filtered.vcf` | VCF | Same variants (full / filtered). |
| `<pseudo>_Mutation_Report1_Statistics.txt` | key/value | Total mutations; homo/hetero; substitutions/insertions/deletions (+in-frame); Ts/Tv ratio. |
| `<pseudo>_Coverage_Curve_Report1_Statistics.txt` | key/value | Total/Aligned reads (+%), Reads on Target, Min/Max/**Average Coverage**, **% ROI > 100x**, bases in ROI, **BED file name**, region count. |
| `<pseudo>_Coverage_Curve_Report1.txt` / `_LowCvrgRegion1.dat` | text | Per-region coverage curve / low-coverage regions. |
| `<pseudo>_SummaryReport.pdf` | PDF | Human-readable clinical summary. |
| `*_settings.txt`, `Settings/*.ini`, `PostProcessingTables*` | text/INI | Report/processing settings. |

### `backups/`, `errors/`, `logs/` (top level)
Not part of the year tree — **exclude from the API.** Whole *source* runs kept after organise /
that failed to organise / organiser logs.

---

## 2. What one predictive number resolves to (sequencing view)

```
predictive number  (mmci_predictive_<uuid>)
  ├── appears in 0..N SEQUENCING RUNS ... every Samples/<pseudo>/ across the tree
  │       each run → FASTQ files + the run's metadata (platform, instrument, date, chemistry, reads)
  │                + the SampleSheet [Data] row (S-number, indexes, Sample_Type RNA/DNA)
  └── has 0..N ANALYSES ................. Samples/<pseudo>/Analysis/  [MiSeq complete-runs only]
          each → BAM, VCF, variant list, coverage/mutation QC
```

- **A predictive number is not unique to one run.** The same folder recurs across many run
  directories (re-sequencing — confirmed extensive across 2023–2024). The API globs the tree and
  returns **all** runs/analyses for the number.
- **Patient/sample identity** for that number comes from the Export Patient API, not from here.

Availability of sequencing data by machine/subtype:

| | FASTQ | Analysis (BAM/VCF/QC) |
|---|:--:|:--:|
| MiSeq `complete-runs` | ✅ | ✅ |
| MiSeq `mamma-print` | ✅ | ❌ |
| MiSeq `missing-analysis` | ✅ | ❌ |
| **NextSeq** | ✅ (≤4 lanes, RNA/DNA) | ❌ |

The response model must represent "FASTQ only, no analysis."

---

## 3. Sequencing field inventory (what data we have)

### 3.1 Run-level metadata (per run, from RunInfo / RunParameters / SampleSheet / CompletedJobInfo)
| Field | Source | Notes |
|---|---|---|
| `run_id` | folder name / `RunInfo Run@Id` | `240104_M02340_0399_000000000-LCBRW` |
| `run_number` | `RunInfo Run@Number` / `RunParameters` | e.g. 399 |
| `machine` | folder (MiSEQ/NextSeq) | derived |
| `instrument_id` | `RunInfo Instrument` / `ScannerID` | e.g. `M02340`, `NB552710` |
| `run_date` | `RunInfo Date` / `RunParameters` | `YYMMDD` |
| `flowcell_id` | `RunInfo Flowcell` | |
| `flowcell_layout` | `RunInfo FlowcellLayout` | LaneCount, SurfaceCount, SwathCount, TileCount |
| `reads` | `RunInfo Reads` | list of (NumCycles, IsIndexedRead) — the read/index structure |
| `platform` / `application` | `SampleSheet [Header] Application` / `RunParameters ApplicationName` | MiSeq vs NextSeq FASTQ Only |
| `assay` / `workflow` | `SampleSheet [Header]` | e.g. `KAPA HyperPlus`, `GenerateFASTQ` |
| `experiment_name` | `SampleSheet [Header]` / `RunParameters ExperimentName` | e.g. `HyperCap-EP-240103` (drives panel lookup) |
| `chemistry` / `reagent_kit` | `RunParameters` / `SampleSheet` | e.g. `Amplicon`, `NextSeq High` |
| `start_time` / `completion_time` | `CompletedJobInfo` (MiSeq) | |
| run QC (cluster PF, cluster density, estimated yield, %Q30, num lanes, error) | `GenerateFASTQRunStatistics.xml` (MiSeq) / `RunCompletionStatus.xml` (NextSeq) | %Q30 MiSeq only |

### 3.2 Per-sample sequencing fields (per Samples/<pred>/ in a run)
| Field | Source | Notes |
|---|---|---|
| `predictive_number` | folder name / `SampleSheet Sample_ID` | `mmci_predictive_<uuid>` |
| `sample_index_number` | FASTQ `S<n>` / sample-sheet order | position in the run |
| `sample_type` | `SampleSheet [Data] Sample_Type` | **NextSeq only**: RNA / DNA |
| `i7/i5 index` | `SampleSheet [Data]` index columns | barcodes |
| `fastq_files[]` | `FASTQ/` | per file: lane, read (R1/R2), path, size |
| `median_read_depth` (`avReadDepth`) | `_Coverage_Curve_Report1_Statistics` / `_StatInfo` | MiSeq; null for NextSeq |
| `observed_read_length` (`obsReadLength`) | analysis | MiSeq; null for NextSeq |
| alignment stats | `_StatInfo.txt` | Matched / Aligned / Perfect Reads |
| coverage stats | `_Coverage_Curve_Report1_Statistics.txt` | Total/Aligned reads, Reads on Target, Min/Max/Avg Coverage, % ROI>100x, bases in ROI, BED file, region count |
| mutation stats | `_Mutation_Report1_Statistics.txt` | total/homo/hetero, subs/ins/del, Ts/Tv |
| file references | `Analysis/` | BAM+bai, VCF (full+filtered), SummaryReport PDF |

### 3.3 Variant columns (`Mutation_Report1.txt`, one row per variant)
`Index, Chrom, Pos, Coverage, Alt%, Ref, Alt, Ref#(F;R), Alt#(F;R), Trans Accession,
Mutation Call: Relative To CDS, Function, Gene, Strand, Exon, Read Balance(Percentage),
SNP db_xref, Amino Acid Change, Clinical Significance, Cosmic:ID`. Multi-allelic rows pack
comma-separated values (e.g. `Alt = "AA,-"`, two `Alt%`). Same variants also in the VCF.

### 3.4 Library / panel metadata (required — lives outside the run folder)
Which **panel** a sample was prepared and sequenced with — and therefore which genes / target
regions (BED) it covers — is **not** stored in the run folder. It comes from a separate,
manually-maintained **Libraries table** in the sibling directory `/muni-sc/Libraries/`:

```
/muni-sc/Libraries/
├── LibrariesV<YYMMDD>.csv        # versioned; the pipeline uses the LATEST by mtime
├── LibrariesV<older>.csv         # older versions kept
└── BEDs/                          # target-region BED files referenced by the table & coverage reports
    ├── MMCI_MOP_2022d_capture_targets.bed
    ├── TSO500bedTargetVisible.bed
    └── …
```

**CSV format:** `;`-delimited, **CP1250** encoded, Czech booleans `PRAVDA`=true /
`NEPRAVDA`=false. Columns (schema varies by version — see edge cases):
`Panel, Text in parameters, code in the molgenis catalogue, Availability Date Range,
Genes (*all coding regions covered), Vendor, Abbreviation, Library Preparation Kit, PCR Free,
Target Enrichment Kit, UMIs Present, BED file` — older versions also had `Input Amount`,
`Intended Insert Size`, `Intended Read Length`.

**Fields the pipeline extracts** (per sample): `input_amount`, `library_prep_kit`,
`pcr_free` (bool), `target_enrichment_kit`, `umi_present` (bool), `intended_insert_size`,
`intended_read_length`, `genes` (full gene list the panel covers), `bed_file` (target regions),
plus the panel name/abbreviation and vendor.

**How a sample is matched to a panel** (from `manage_libraries.py`), in order:
1. **From the analysis parameters file** — read the last line of the sample's
   `Analysis/…/<pseudo>_Parameters.txt` and match it against the CSV `Text in parameters`
   column. (MiSeq only; NextSeq has no Parameters.txt.)
2. **Fallback — from `SampleSheet` `Experiment Name`** (e.g. `HyperCap_240103`): split into
   `<name>_<date>`, map the name via a small alias table (`SeqCapH`→HyperCap, `EG`→EliGene,
   `TSO500`→TruSight), then pick the panel whose `Panel` matches **and** whose
   `Availability Date Range` contains the run date. Ambiguity falls back to a `manual` row.

The **BED file** links back to the coverage QC: `Coverage_Curve_Report1_Statistics.txt` names the
BED it used (§3.2), and that BED lives in `/muni-sc/Libraries/BEDs/`.

---

## 4. Proposed sequencing data model

**Design goal: biobank-agnostic.** The domain must fit *any* site with sequencing data — MMCI's
`/muni-sc/OrganisedRuns` tree is merely the **first source**. So the domain carries no
MMCI / Illumina / NextGENe specifics: those live in an **ingestion adapter** (§4.3) and in open
lookup tables + per-entity extensible attribute bags (§4.2). Sequencing-scoped — **no
patient/clinical entities** (joined in from the Export Patient API).

```
                       Panel ───referenced by───┐
                    (target panel, shared)      │
                                                ▼
Sample ──1:N── RunSample ──0:1── LibraryPreparation
(the sequenced   (sample in a run)
 subject, keyed        │  │
 by external id)       │  └──1:N── SequencingFile   (fastq/bam/vcf/report/… via `role`)
                       │  │
      SequencingRun ───┘  └──0:N── Analysis ──1:N── Variant
      (a run/flowcell)         (a pipeline run)  (VCF-aligned record)
                                     │
                                     └──1:N── QcMetric   (typed core + open name/value bag)
```

`Sample` aggregates across runs (one sample → many runs → many analyses). Every relationship the
MMCI tree expresses is preserved, but nothing about the model is MMCI-specific.

### 4.1 Core entities (generic)

**Sample** — the sequenced biological sample, tracked across runs by an **opaque external id**.
PK `sample_id` (internal). Columns: `external_id`, `id_scheme` (e.g. `mmci_predictive`),
`subject_ref` (opaque pointer to the patient in the external system — patient data stays out of
scope), `attributes` (JSON). *MMCI:* `external_id = mmci_predictive_<uuid>`, `id_scheme =
'mmci_predictive'`.

**SequencingRun** — one run / flowcell. PK `run_id`. Columns: `platform` (Illumina/…),
`instrument_model`, `instrument_id`, `run_number`, `run_date`, `flowcell_id`, `flowcell_layout`,
`read_structure`, `chemistry`, `reagent_kit`, `experiment_name`, `workflow`, `source_class`
(open enum — MMCI: MiSeq/NextSeq + complete-runs/mamma-print/missing-analysis), `started_at`,
`completed_at`, `run_metadata` (JSON — vendor extras). Source: run-level XML/CSV.

**RunSample** — a sample sequenced in a run (the join enabling one sample → many runs). PK
(`run_id`, `sample_id`). Columns: `sample_index`, `sample_type` (open enum, e.g. DNA/RNA),
`indexes` (barcodes), `lane_count`, `attributes` (JSON). Source: sample folder + sample-sheet row.

**SequencingFile** — a generic file artifact, FK → RunSample or Analysis. Columns: `role` (open
enum: `fastq` / `bam` / `bam-index` / `vcf` / `vcf-filtered` / `coverage-report` / `summary-pdf`
/ …), `format`, `uri`/`path`, `read` (R1/R2, fastq), `lane` (fastq), `size`, `checksum`. **One
generic table instead of per-format columns** — new artifact kinds need no schema change.

**LibraryPreparation** — 0..1 per RunSample; how the sample was prepped. FK → RunSample, `panel_id`.
Columns: `input_amount`, `library_prep_kit`, `pcr_free`, `target_enrichment_kit`, `umi_present`,
`intended_insert_size`, `intended_read_length`, `attributes` (JSON). Nullable (panel may be
unresolved). *MMCI source:* the matched Libraries CSV row (§3.4).

**Panel** — a target panel, shared by many samples. PK `panel_id`. Columns: `name`, `vendor`,
`assay`, `genes[]`, `target_regions_ref` (BED/interval list), `version`/`availability`,
`attributes`. The `target_regions_ref` links to observed coverage in QC.

**Analysis** — 0..N per RunSample; a bioinformatic analysis. PK `analysis_id`. Columns:
`analysis_type` (open enum: variant-calling / expression / fusion / CNV / …), `pipeline_name`,
`pipeline_version`, `reference_genome`, `produced_at`, `parameters` (JSON), `attributes`. *MMCI:*
`pipeline_name = NextGENe`, `analysis_type = variant-calling`, `reference_genome = GRCh37`.

**Variant** — 0..N per Analysis; **VCF-aligned** so any caller maps cleanly. PK synthetic. FK →
Analysis. Columns: `chrom`, `pos`, `ref`, `alt`, `gene`, `transcript`, `hgvs_c`, `hgvs_p`,
`consequence`, `coverage`, `allele_fraction`, `ref_depth`, `alt_depth`, `strand`, `exon`,
`dbsnp`, `clinical_significance`, `cosmic_id`, `annotations` (JSON). *MMCI source:*
`Mutation_Report1.txt`/`.vcf` (map NextGENe columns onto these).

**QcMetric** — QC for a RunSample and/or Analysis. **Two-part for extensibility:**
- *(a) typed common core* (cross-biobank, queryable): `average_coverage`, `pct_target_over_100x`,
  `median_read_depth`, `observed_read_length`, `percentage_q30`, `total_reads`, `aligned_reads`,
  `on_target_rate`, `total_variants`, `ts_tv_ratio`, `overall_qc` (pass/warn/fail).
- *(b) open bag* `QcMetric(owner, name, value, unit, scope)` for anything site-specific — no
  schema change to add a metric. *MMCI source:* `_StatInfo` / `_Coverage_Curve` / `_Mutation_*`
  statistics.

### 4.2 Extension points (what makes it biobank-generic)
- **Opaque identifiers + `id_scheme`** — never hardcode `mmci_predictive_`; another site uses its
  own scheme.
- **Open enumerations as lookup tables** — `platform`, `source_class`, `sample_type`,
  `analysis_type`, file `role`, `overall_qc` — add values without code changes.
- **`attributes` JSON bag on every entity** + the **`QcMetric` name/value** model — attach
  site-specific fields with zero schema migration.
- **Generic `SequencingFile.role`** — FASTQ / BAM / CRAM / VCF / reports / any future artifact.
- **Caller-agnostic `Analysis` + VCF-aligned `Variant`** — NextGENe, DRAGEN, GATK, etc. all fit.
- **The ingestion-adapter boundary** (below) keeps all source quirks out of the domain.

### 4.3 Source mapping / ingestion adapter (MMCI = first source)
The domain knows nothing about the folder tree, NextGENe report text, or the Libraries CSV. A
**per-biobank ingestion adapter** maps *source → domain*:

| Source (MMCI) | → Domain |
|---|---|
| `OrganisedRuns` run folder + run XML/CSV | `SequencingRun` |
| `Samples/<pred>/` + sample-sheet row | `Sample` + `RunSample` |
| `FASTQ/`, `Analysis/` BAM/VCF/PDF files | `SequencingFile` (by `role`) |
| NextGENe `_StatInfo` / `_Coverage` / `_Mutation_*` stats | `QcMetric` |
| `Mutation_Report1.txt` / `.vcf` rows | `Variant` |
| Libraries CSV (+ BEDs) | `LibraryPreparation` + `Panel` |
| `catalog_info_per_pred_number/` | **not ingested** — patient data via Export Patient API |

Another biobank supplies its own adapter to the same domain; nothing downstream changes. All the
MMCI parsing quirks (ISO-8859 encoding, comma decimals, multi-allelic rows, versioned Libraries
CSV) are the adapter's concern, not the schema's.

> Design note: the *file references*, *QC metrics*, and *variants* are the substance of the API.
> The old uploader stored only paths + protocol and dropped variants/QC — this model keeps them
> (they already exist on disk) while staying vendor-neutral.

---

## 5. Quality & statistics endpoints

The tree already contains everything needed to answer rich operational and QC questions — the
model just has to **capture a few derived fields at ingest** so the stats don't require a full
filesystem scan on every request. (Scanning is expensive: a whole-tree `find` for sample folders
times out; e.g. 2024 MiSeq complete-runs alone = 47 runs / 757 samples, NextSeq = 18 runs / 287
samples, yet only ~10 runs carry the `.uploaded` marker — most are pending.)

### 5.1 Use the `Sample` aggregate
`RunSample` is per-run; most stats are per **sample** (MMCI: per predictive number). The `Sample`
entity (§4.1) is the aggregate: give it derived columns `run_count`, `analysis_count`,
`first_sequenced`, `last_sequenced`, `latest_qc`, `platforms_used`, `panels_used`, and a computed
**readiness** (below). This is the natural unit for "how many samples/predictive numbers …"
questions and for the re-sequencing metric — and it stays biobank-generic (keyed by `external_id`,
not an MMCI-specific field).

### 5.2 Fields to add to the model (cheap, computed at ingest)
- **Completeness flags** on `RunSample`: `has_fastq`, `has_analysis`, `has_library`,
  `fastq_lane_count`, `is_complete`.
- **Upload / ingest state**: `upload_state` (`pending` / `uploaded` / `error`) + `uploaded_at`,
  derived from the run's `.uploaded` marker (and its mtime); `blocking_reason` when not uploadable
  (e.g. `missing-analysis`, `panel-unresolved`, `no-clinical-in-patient-api`).
- **QC pass/fail flags** on `QcMetrics`, from configurable thresholds:
  `coverage_pass` (e.g. avg ≥ 500× or % ROI>100x ≥ 95%), `q30_pass`, `on_target_pass`,
  `overall_qc` (pass/warn/fail). Keep the raw metrics too so thresholds can change.
- **Timestamps for time-series**: `sequenced_at` (run date — already have), `organised_at`,
  `ingested_at`, `uploaded_at`.
- **An `IngestEvent` audit table** (append-only): `(predictive_number, run_id, event_type,
  timestamp, detail)` where `event_type` ∈ discovered / parsed / qc-computed / uploaded / error.
  This one table powers *all* throughput-over-time and status-transition stats.
- Optionally **materialized summary counters** (per run / per day / per panel) refreshed at
  ingest, so stats endpoints are O(1) instead of scanning.

### 5.3 Good statistics questions the API can then answer

**Readiness / upload pipeline** (the "how many can be uploaded" family)
- How many **distinct predictive numbers** exist? How many are **uploadable now** (complete +
  not yet uploaded)? How many **already uploaded**? How many **blocked**, and by which reason?
- Breakdown of the above by **machine** (MiSeq/NextSeq), **subtype** (complete / mamma-print /
  missing-analysis), and **year**.
- How many runs are in **`errors/`** (failed to organise)?

**Sequencing quality (aggregate QC)**
- Distribution (median / p10 / p90) of **average coverage**, **% ROI > 100×**, **median read
  depth**, **%Q30** (MiSeq); count of samples **failing** each threshold.
- **On-target rate** distribution; samples with low reads-on-target.
- Variants per sample: mean/percentiles of **total mutations**, **Ts/Tv ratio**, homo/hetero split.

**Throughput / operational**
- **Predictive numbers sequenced per month/quarter**; runs per month; **avg samples per run**.
- **Re-sequencing rate**: how many predictive numbers appear in **> 1 run**, and the distribution
  of runs-per-number (a data-quality signal).
- Time from `sequenced_at` → `uploaded_at` (pipeline latency).

**Panel / assay usage**
- **Samples per panel** and per assay; panel mix **over time**; samples with an **unresolved
  panel** (library-match failure).
- Gene-panel coverage: which genes/BED were targeted, and coverage achieved against them.

**Data integrity**
- Samples **missing analysis** where it was expected; runs **missing metadata** files; parse /
  encoding failures logged as `IngestEvent(error)`.
- Storage footprint per year / run (the organiser already emits `organised_runs_du.txt`).

### 5.4 Suggested endpoints
- `GET /stats/summary` — totals: predictive numbers, uploadable, uploaded, blocked (with reasons).
- `GET /stats/quality?panel=&from=&to=` — coverage/Q30/variant distributions + threshold fail counts.
- `GET /stats/throughput?granularity=month` — sequenced vs uploaded over time; re-sequencing rate.
- `GET /stats/panels` — usage counts, panel mix over time, unresolved-panel count.
- `GET /sample/{external_id}` — everything for one sample (the core endpoint; MMCI calls it by
  predictive number) incl. its runs, files, analyses, variants, readiness + latest QC.

---

## 6. Edge cases & data-quality hazards (sequencing side)

All of these were observed in the live 2024/2019 tree — treat them as *will happen*, not *might*.
The recurring lesson: **the folder tree, not the SampleSheet, is the source of truth for what
sequencing data exists**, and almost every "always" assumption (paired-end, 2 FASTQ, sheet = folders,
a run is in one place) is violated somewhere.

### 6.1 Presence & completeness — data you expect can be missing
- **`Samples/` contains empty orphan folders not in the SampleSheet.** Real case: run `240626…`
  has 16 sheet entries **plus 16 extra `mmci_predictive_` folders that are completely empty**
  (0 files) → 32 folders for 16 samples. **5 runs** show this (all in 2024). So the sheet is
  **not** a reliable index of `Samples/`, and a sample folder may contain **nothing**. Enumerate
  folders *and* reconcile against the sheet; skip/flag empties.
- **A sample folder can have an empty `FASTQ/` (0 reads)** even when the folder exists. This is
  **not rare — 113 such sample folders across every year** (2019→2, 2020→12, 2021→8, 2023→22,
  2024→67, 2025→2). "Folder exists" ≠ "has reads."
- **NextSeq / mamma-print / missing-analysis = FASTQ only** — no per-sample analysis, no BAM/VCF/QC,
  and (NextSeq) no `catalog_info`. Model a sample that has reads but no analysis.
- **Empty (zero-byte) files occur.** A `catalog_info` JSON was 0 bytes (`240510…`). Any file (JSON,
  report, even FASTQ) can be empty — guard every parse against empty/malformed input.

### 6.2 Reads & FASTQ — structure is not fixed
- **Not every run is paired-end — 89 single-read (R1 only) runs**, which are **essentially all the
  MammaPrint runs** (plus one complete-run, `240430…`). A single-read run's `RunInfo.xml` has one
  non-index template read, so each sample has **1** FASTQ, not 2. **Read the RunInfo read structure;
  don't assume R1+R2** — and expect MammaPrint = single-end.
- **FASTQ-per-sample varies:** MiSeq paired = 2, MiSeq single-end = 1, NextSeq = up to 8 (4 lanes ×
  R1/R2), and 0 when demux produced nothing. Derive from RunInfo (reads × lanes), not a constant.
- **NextSeq `Sample_Type` (DNA/RNA) is load-bearing.** In a run each predictive number is *either*
  DNA *or* RNA (a run splits ~8/8), and they feed different analyses (variants vs fusion/expression).
  The *same* sample may be sequenced as both DNA and RNA across different runs — aggregate by
  `sample_type`, don't collapse them.

### 6.3 Runs, `errors/`, and duplication — reconcile by run id
- **The same run id can be in BOTH `errors/` and the organised tree — 100% of `errors/` runs** (all
  7) also have a tree copy. The `errors/` copy is the **raw, unorganised Illumina run** (`Data/`,
  `Config/`, `Alignment_1/`, `CopyComplete.txt`, `CompletedJobInfo.xml`, **no `Samples/`**), i.e. a
  re-processing leftover — not an alternate copy of the organised data. **Dedup by run id; never
  serve `errors/`/`backups/`; a run being in `errors/` does not mean it is absent from the tree.**
- **The same run id can even be in TWO subtype folders** — `240430…` exists under **both**
  `complete-runs/` *and* `mamma-print/`. So run id is not unique to one path; a run may be
  discovered more than once within the tree itself. Dedup, and pick the copy with data.
- **A predictive number recurs across many runs** (re-sequencing) — aggregate every occurrence.
- **`.uploaded`** marks a run already consumed by the old uploader (state, not data).

### 6.4 Encoding & text parsing
- **NextGENe `.txt`/logs are ISO-8859 / Windows-1250, not UTF-8**, with **mixed CRLF/CR** endings
  and Czech words (`pátek`, `leden`). Decode explicitly; UTF-8 will mojibake or raise.
- **Decimal commas** everywhere in NextGENe reports: `96,592%`, `1001,43`, `2,040`. Parse
  locale-aware — `float("96,592")` fails.
- **Multi-allelic variant rows** pack comma-separated values into `Alt`, `Alt%`, `Alt#(F;R)` and the
  CDS/function columns — split them when normalising into `Variant`.

### 6.5 Libraries / panel resolution
- **Libraries table is external, versioned, latest-by-mtime** (`/muni-sc/Libraries/Libraries*.csv`):
  **CP1250**, `;`-delimited, Czech booleans (`PRAVDA`/`NEPRAVDA`), and **its column set differs
  between versions** (newer files dropped `Input Amount` / `Intended Insert Size` /
  `Intended Read Length`). Don't assume a fixed schema; editing the CSV retroactively changes
  historical panel data.
- **`Experiment Name` has no consistent format — 16+ distinct spellings across the corpus**, e.g.
  `HyperCap_241210` (2 parts), `HyperCap241217` / `SeqCapH240101` (no separator), `HyperCap-EP-240103`
  (hyphens), `TSO500_Run2024_9` / `MP_18_2024` (3 parts), plus `HypCap…`, `SeqCap…`, `Accel…`
  variants. The date is embedded (`YYMMDD`) or separated by `-`/`_`, with **0–3 underscores**, so the
  old matcher's `split("_")`-into-two **breaks on the majority**. Treat the per-sample
  **`_Parameters.txt`** as the reliable panel signal (MiSeq); the Experiment-Name/date-range path is
  best-effort only.
- **More panel families than the alias table knows** — the corpus contains HyperCap/HypCap,
  SeqCap/SeqCapH, Accel, TSO500, MammaPrint (MP), EliGene (EG); the old alias map only covers
  `SeqCapH`/`EG`/`TSO500`, so others rely on substring/`_Parameters.txt` matching.
- **Panel matching can fail** → `LibraryPreparation` must be **nullable**; expect the alias table
  (`SeqCapH`→HyperCap, `EG`→EliGene, `TSO500`→TruSight) and date-range logic.

### 6.6 Out of scope (reminders)
- **`catalog_info` clinical JSON is present but not this API's job** — patient/sample data comes from
  the Export Patient API. (Also: it has its own hazards — masked vs full birth, string vs int `sex`,
  ISO vs `dd/mm` dates, leaked `RetrievalType.` enum — which the *patient* API/uploader path must
  handle, not this one.)

---

## 7. Reference fixtures

Sanitized, zero-UUID example runs in the organiser test suite (use these, not real dirs):
- `data-catalogue-organiser/tests/test_pseudonymized_runs/240101_M00000_0000_00000000-00000/`
  — **New MiSeq** complete run (`Alignment_1/`, `Analysis/…_Output/`).
- `.../200101_M00000_0000_00000000-00000/` — **Old MiSeq** complete run.
- `.../230101_N0000000_0000_0000000000/` — **NextSeq** run (`FASTQ/`).

Real dirs inspected: `2024/MiSEQ/complete-runs/240104_M02340_0399_000000000-LCBRW`,
`2024/MiSEQ/mamma-print/…`, `2024/MiSEQ/missing-analysis/240522_M02340_0437_…`,
`2024/NextSeq/240102_NB552710_0064_AHG7LGBGXV`, `2019/MiSEQ/complete-runs/191128_M02340_0216_000000000-CNK2N`.
