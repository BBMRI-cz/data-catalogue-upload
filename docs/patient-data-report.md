# Biobank XML Export — Data Report

> Status: complete.

---

## 1. Overview

| Property | Value |
|---|---|
| Source directory | `/home/mou/patient_data/` |
| Filtered directory | `/home/mou/patient_data_filtered/` |
| Biobank | MOU (Masaryk Oncology Institute) |
| Namespace | `http://www.bbmri.cz/schemas/biobank/data` |
| XSD schema ref | `exportNIS.xsd` (referenced in each file) |
| Encoding | UTF-8 |

---

## 2. Dataset Size

| Dataset | File count | Disk size |
|---|---|---|
| `patient_data/` (full) | 892,997 | 3.5 GB |
| `patient_data_filtered/` | 534,775 | 2.1 GB |
| Excluded (full minus filtered) | 358,222 | ~1.4 GB |

Individual file sizes range from ~280 B (self-closing patient stub) to ~6 KB (patient with many LTS samples). Median is ~590 B, average ~830 B.

---

## 3. Filename Convention

```
BBM{YYMMDD}{batch_id}-{seq_num}.XML
```

Examples:
- `BBM220925230002-000003.XML` — batch exported 2022-09-25, batch ID `230002`, patient sequence `000003`
- `BBM260308230015-000039.XML` — batch exported 2026-03-08

| Property | Value |
|---|---|
| Prefix | `BBM` (fixed) |
| Date part | `YYMMDD` (export date) |
| Batch ID | 6-digit number appended to date |
| Patient sequence | 6-digit zero-padded within batch, separated by `-` |
| Extension | `.XML` (uppercase) |

There are **169 unique export batches** spanning from **2022-09-25 to 2026-03-08**.

---

## 4. Structural Categories

Files fall into four structural categories:

| Category | Count | Description |
|---|---|---|
| Self-closing patient stub | ~272,000 | `consent="false"` patients with no child elements |
| Empty `<LTS/>` + `<STS>` | 481,065 | Diagnostic sample recorded but nothing in long-term storage |
| Empty `<LTS/>`, no `<STS>` | 86,037 | Patient registered, no samples at all |
| Non-empty LTS (stored samples) | 53,698 | Has `<genome>`, `<serum>`, or `<tissue>` in LTS |

Some patients with LTS samples also have STS (~18,500 files). AccessionNumbers appear in ~396,000 files.

#### Category 1 — consent=false stub

Patient declined consent. No data beyond demographics.

```xml
<patient biobank="MOU" consent="false" id="271801" month="--07" sex="male"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1948"/>
```

#### Category 2 — empty LTS + STS

Diagnostic sample taken and recorded, but nothing archived in the biobank for research. `<STS>` (Short-Term Storage) records that a specimen was processed for diagnosis — it is consumed during that process, not kept. `<LTS/>` is empty because no research sample was collected that visit.

```xml
<patient biobank="MOU" consent="true" id="247" month="--02" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1957">
  <LTS/>
  <STS>
    <diagnosisMaterial number="118485" sampleId="&amp;:2022:118485" year="2022">
      <materialType>S</materialType>
      <diagnosis>C504</diagnosis>
      <takingDate>2022-09-20T10:44:00</takingDate>
      <retrieved>unknown</retrieved>
    </diagnosisMaterial>
  </STS>
</patient>
```

#### Category 3 — empty LTS, no STS

Patient registered in the system but no sample activity of any kind.

```xml
<patient biobank="MOU" consent="true" id="13452" month="--04" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1947">
  <LTS/>
</patient>
```

#### Category 4 — non-empty LTS

Samples actually archived in the biobank for future research.

```xml
<patient biobank="MOU" consent="true" id="138423" month="--05" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1943">
  <LTS>
    <serum biopsy="-" number="3249" predictive_number="-" sampleId="BBMs:2022:3249:SD" year="2022">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>SD</materialType>
      <takingDate>2022-12-07</takingDate>
    </serum>
  </LTS>
  <STS>
    <diagnosisMaterial number="155548" sampleId="&amp;:2022:155548" year="2022">
      <materialType>S</materialType>
      <diagnosis>Z129</diagnosis>
      <takingDate>2022-12-07T07:35:00</takingDate>
      <retrieved>unknown</retrieved>
    </diagnosisMaterial>
  </STS>
</patient>
```

### Filtering logic

`patient_data_filtered/` excludes all files where `consent="false"` or where the patient has only an empty `<LTS/>` and no `<STS>` — i.e., files with no clinically actionable data.

---

## 5. Patient Demographics (from sample)

| Field | Observed values |
|---|---|
| `biobank` | `MOU` (always) |
| `consent` | `true` (~70%), `false` (~30%) |
| `sex` | `female` (~77%), `male` (~23%) |
| `year` (birth year) | 1928–2023 |
| `month` (birth month) | `--01` through `--12` (ISO 8601 `--MM` format) |
| `id` | Numeric internal patient ID |

The dataset is heavily female-skewed, consistent with MOU's oncology focus on breast, cervical, and ovarian cancers.

---

## 6. XML Element & Attribute Reference

---

### 6.1 `<patient>` — root element

Every file contains exactly one `<patient>` element. It can be self-closing (when `consent="false"` and there is no data to export) or contain child elements.

#### Attributes

| Attribute | Type | Required | Format / Values | Notes |
|---|---|---|---|---|
| `biobank` | string (≤6) | yes | `MOU` | Always `MOU` in this dataset (Masaryk Oncology Institute) |
| `consent` | boolean | yes | `true` / `false` | Informed consent. When `false` the element is self-closing — no clinical data is exported. |
| `id` | string (≤10) | yes | numeric string, e.g. `247` | Internal hospital patient identifier |
| `year` | xs:gYear | yes | `YYYY`, range 1928–2023 | Patient birth year |
| `month` | xs:gMonth | **no** | `--MM`, e.g. `--02` | Patient birth month. ISO 8601 gMonth format (double dash prefix, no day/year). Absent in some records. |
| `sex` | enum | yes | `male` / `female` | Patient sex. Roughly 77% female, 23% male in this dataset. |
| `xmlns` | string | yes | `http://www.bbmri.cz/schemas/biobank/data` | XML namespace, always this value |
| `xsi:noNamespaceSchemaLocation` | string | yes | `exportNIS.xsd` | Schema reference, always this value |

#### Child elements (in document order)

| Element | Occurrences | Description |
|---|---|---|
| `AccessionNumbers` | 0..1 | Patient-level radiology accession numbers |
| `LTS` | 0..1 | Long-Term Storage samples archived in the biobank |
| `STS` | 0..1 | Short-Term Storage — diagnostic specimen records |

---

### 6.2 `<AccessionNumbers>` (patient-level)

Optional. Present in ~415,000 files. Holds radiology accession numbers that link the patient to imaging studies performed at MOU.

Can be empty (`<AccessionNumbers/>`) or contain one or more `<Number>` child elements.

#### `<Number>` child element

| Property | Detail |
|---|---|
| Content | Single accession number string |
| Format | `RDG{YYYY}{6-digit}`, e.g. `RDG2006028299` |
| Prefix | Always `RDG` (radiology) in observed data |
| Count per patient | 1 to 20+ numbers observed |

```xml
<AccessionNumbers>
  <Number>RDG2005041156</Number>
  <Number>RDG2019048259</Number>
  <Number>RDG2021047058</Number>
</AccessionNumbers>
```

---

### 6.3 `<LTS>` — Long-Term Storage

Optional. Present in all files except self-closing patient stubs (consent=false). Can be empty (`<LTS/>`), which means the patient is registered but no samples have been archived.

When non-empty, contains any combination of `<tissue>`, `<serum>`, `<genome>`, and `<diagnosisMaterial>` elements. Multiple elements of the same type are common — one per material type collected from a single event.

---

### 6.3.1 Common sample attributes (`tissue` / `serum` / `genome`)

All three LTS sample types share the same attribute group:

| Attribute | Type | Required | Format / Values | Notes |
|---|---|---|---|---|
| `year` | xs:gYear | yes | `YYYY`, e.g. `2023` | Year the sample was collected (not birth year) |
| `number` | string (≤6) | yes | integer, e.g. `524` | Internal biobank sample event number. All aliquots from the same collection event share the same `number`. |
| `sampleId` | string (≤32) | yes | See format table below | Unique composite identifier for this specific aliquot type |
| `biopsy` | string | yes | `YYYY/{number}-{part}` or `"-"` | **Pathology lab reference.** Links the sample to a pathology biopsy case. Format: `2023/2872-1` = year 2023, case 2872, block/part 1. Value `"-"` means no pathology biopsy associated. |
| `predictive_number` | string | yes | `YYYY/{number}` or `"-"` | **Digital pathology / sequencing reference.** Links the sample to a predictive medicine / sequencing request. Format: `2023/1052`. Value `"-"` means no sequencing requested. |

#### `sampleId` format by element type

| Element | sampleId prefix | Full format | Example |
|---|---|---|---|
| `tissue` | `BBM:` | `BBM:{year}:{number}:{materialType}` | `BBM:2023:181:1` |
| `serum` | `BBMs:` | `BBMs:{year}:{number}:{materialType}` | `BBMs:2023:524:SD` |
| `genome` | `BBMd:` | `BBMd:{year}:{number}:{materialType}` | `BBMd:2023:249:PK` |
| `diagnosisMaterial` (STS) | `&amp;:` | `&:{year}:{number}` (no materialType) | `&:2022:118485` |

The `sampleId` encodes the biobank (`BBM`), the collection year, the event number, and the material type. The `s` suffix in `BBMs` = serum store; `d` suffix in `BBMd` = DNA store. The STS prefix is a literal `&` (XML-escaped as `&amp;`) and does not include materialType.

---

### 6.3.2 `<tissue>` — surgical/tumour tissue

Frozen tissue samples from surgical resection or biopsy, stored in the LTS freezers.

#### Child elements

| Element | Required | Type | Notes |
|---|---|---|---|
| `AccessionNumbers` | no | container | Present in newer files (post-2024) even when empty (`<AccessionNumbers/>`). Older files omit it. Contains `<Number>` elements. |
| `samplesNo` | yes | integer | Total number of aliquots (blocks/vials) created |
| `availableSamplesNo` | yes | integer | Number of aliquots currently available (not yet consumed) |
| `materialType` | yes | string code | Tissue type code — see table below |
| `pTNM` | no | string | Pathological TNM staging, e.g. `T1N0M`, `T3N1M`. Format is free text — staging completeness varies (some values omit M stage: `T1NM`). |
| `morphology` | no | string (≤7) | ICD-O-3 morphology code, e.g. `8500/32`. Format: `{4-digit morphology}/{behaviour}`. |
| `diagnosis` | no | string (≤6) | ICD-10 diagnosis code at time of sampling, e.g. `C504`. |
| `cutTime` | yes | xs:dateTime or xs:date | Timestamp when the surgical specimen was cut/removed. Usually datetime, occasionally date only. |
| `freezeTime` | yes | xs:dateTime or xs:date | Timestamp when the sample was frozen. Usually a few minutes after `cutTime`. |
| `retrieved` | no | enum | Tissue collection method — `operational` (taken during surgery) or `unknown`. Always present for tissue in observed data. |

#### `materialType` codes for `<tissue>`

| Code | Czech name | English meaning |
|---|---|---|
| `1` | Nádor maligní | Malignant tumour |
| `2` | Metastáza | Metastasis |
| `3` | Nádor benigní | Benign tumour |
| `4` | Zdravá tkáň | Healthy/normal tissue |
| `5` | Premaligní tkáň | Premalignant tissue |
| `53` | Maligní (RNA-LATER) | Malignant tumour preserved in RNAlater |
| `54` | Zdravá (RNA-LATER) | Healthy tissue preserved in RNAlater |
| `55` | Metastáza (RNA-LATER) | Metastasis preserved in RNAlater |
| `56` | Benigní (RNA-LATER) | Benign tumour preserved in RNAlater |
| `7` | PBMNC | Peripheral blood mononuclear cells |

Most common in observed data: `1`, `4`, `53`, `54` (malignant + healthy pairs, with and without RNAlater). Codes `2`, `55` appear when metastatic tissue is collected alongside the primary. Code `3`/`5`/`56` are rare.

A single surgical event typically produces multiple `<tissue>` rows sharing the same `number`, each with a different `materialType` — e.g., one entry for `1` (tumour) and one for `4` (healthy margin), and their RNAlater counterparts `53`/`54`.

---

### 6.3.3 `<serum>` — blood-derived liquid samples

Blood draw samples processed into various fractions and stored in liquid nitrogen.

#### Child elements

| Element | Required | Type | Notes |
|---|---|---|---|
| `AccessionNumbers` | no | container | Same pattern as tissue — empty element in newer files, absent in older. |
| `samplesNo` | yes | integer | Total aliquots created |
| `availableSamplesNo` | yes | integer | Aliquots available |
| `materialType` | yes | string code | Fraction type — see table below |
| `diagnosis` | no | string (≤6) | ICD-10 code. Present when the blood draw was linked to a specific diagnosis event (e.g., linked via `biopsy` to a concurrent tissue collection). |
| `takingDate` | yes | xs:date or xs:dateTime | Date of blood draw. Usually date only (`2023-03-24`), sometimes datetime. |
| `retrieved` | no | enum | `operational` or `unknown`. About 60% of serum elements include it; 40% omit it entirely. |

#### `materialType` codes for `<serum>`

| Code | Czech name | English meaning |
|---|---|---|
| `K` | Plazma K3EDTA dusík | K3EDTA plasma, nitrogen-stored |
| `SD` | Sérum dusík | Serum, nitrogen-stored |
| `S` | Sérum | Serum (room temperature / short-term) |
| `PD` | Plasma dusík | Plasma, nitrogen-stored |
| `L` | Plazma Li-heparin dusík | Li-heparin plasma, nitrogen-stored |
| `T` | Plazma CTAD dusík | CTAD plasma, nitrogen-stored |
| `C` | Plazma se stabilizátorem DNA | Plasma with DNA stabiliser |
| `PR` | Primokultury | Primary cell cultures |

Most common in observed data: `SD` (serum nitrogen) and `K` (K3EDTA plasma). A blood draw event usually produces both — one `<serum>` row per fraction.

---

### 6.3.4 `<genome>` — DNA / nucleic acid samples

Samples collected for genomic/DNA purposes and stored in liquid nitrogen.

#### Child elements

| Element | Required | Type | Notes |
|---|---|---|---|
| `AccessionNumbers` | no | container | Same pattern as tissue/serum. |
| `samplesNo` | yes | integer | Total aliquots |
| `availableSamplesNo` | yes | integer | Available aliquots |
| `materialType` | yes | string code | DNA source type — see table below |
| `takingDate` | yes | xs:date or xs:dateTime | Date sample was taken |
| `retrieved` | no | enum | `operational` or `unknown`. Observed on both values for genome samples. |

#### `materialType` codes for `<genome>`

| Code | Czech name | English meaning |
|---|---|---|
| `PK` | Plná krev | Whole blood (for DNA extraction) |
| `PS` | Plná krev se stabilizátorem DNA | Whole blood with DNA stabiliser (e.g. Streck tube) |
| `gD` | Genomová DNA | Extracted genomic DNA |

Most common: `PK` and `PS`. `gD` is rare in this dataset.

---

---

### 6.4 `<STS>` — Short-Term Storage

Optional. Present in ~499,500 files. Records specimens that were processed for **diagnosis** — typically surgical or blood samples sent to the pathology lab. These are not archived for research; the specimen is consumed during diagnosis. The record exists purely to link the patient's clinical diagnosis to a collection event.

Contains one or more `<diagnosisMaterial>` elements.

---

### 6.4.1 `<diagnosisMaterial>`

#### Attributes

| Attribute | Type | Required | Format / Values | Notes |
|---|---|---|---|---|
| `year` | xs:gYear | yes | `YYYY` | Year the diagnostic specimen was taken |
| `number` | string (≤6) | yes | integer string | Internal sample number in the hospital system |
| `sampleId` | string (≤32) | yes | `&amp;:{year}:{number}` | Prefixed with `&amp;` (XML entity for `&`). No materialType suffix, unlike LTS sampleIds. |

#### Child elements

| Element | Required | Type | Notes |
|---|---|---|---|
| `materialType` | yes | string | Always `S` (Sérum / surgical specimen) in observed data |
| `diagnosis` | yes | string (≤6) | ICD-10 diagnosis code associated with this specimen |
| `takingDate` | yes | xs:dateTime or xs:date | Timestamp of specimen collection. Usually datetime. |
| `retrieved` | yes | enum | Always `unknown` in observed data — diagnostic specimens are not retrieved from storage. |

---

### 6.5 Material Type — full lookup table

All codes across all element types:

| Code | Element(s) | Czech name | English meaning |
|---|---|---|---|
| `1` | tissue | Nádor maligní | Malignant tumour |
| `2` | tissue | Metastáza | Metastasis |
| `3` | tissue | Nádor benigní | Benign tumour |
| `4` | tissue | Zdravá tkáň | Healthy/normal tissue |
| `5` | tissue | Premaligní tkáň | Premalignant tissue |
| `7` | tissue | PBMNC | Peripheral blood mononuclear cells |
| `53` | tissue | Maligní (RNA-LATER) | Malignant tumour in RNAlater |
| `54` | tissue | Zdravá (RNA-LATER) | Healthy tissue in RNAlater |
| `55` | tissue | Metastáza (RNA-LATER) | Metastasis in RNAlater |
| `56` | tissue | Benigní (RNA-LATER) | Benign tumour in RNAlater |
| `gD` | genome | Genomová DNA | Extracted genomic DNA |
| `PK` | genome | Plná krev | Whole blood (for DNA) |
| `PS` | genome | Plná krev se stabilizátorem DNA | Whole blood with DNA stabiliser |
| `K` | serum | Plazma K3EDTA dusík | K3EDTA plasma, nitrogen-stored |
| `SD` | serum | Sérum dusík | Serum, nitrogen-stored |
| `S` | serum / diagnosisMaterial | Sérum | Serum / diagnostic specimen |
| `PD` | serum | Plasma dusík | Plasma, nitrogen-stored |
| `L` | serum | Plazma Li-heparin dusík | Li-heparin plasma, nitrogen-stored |
| `T` | serum | Plazma CTAD dusík | CTAD plasma, nitrogen-stored |
| `C` | serum | Plazma se stabilizátorem DNA | Plasma with DNA stabiliser |
| `PR` | serum | Primokultury | Primary cell cultures |

---

### 6.6 `<diagnosis>` — ICD-10 codes

Appears in `<diagnosisMaterial>`, `<tissue>`, and `<serum>`. Max length 6 characters. Standard ICD-10 code without a dot separator (e.g. `C504` not `C50.4`).

Most frequent codes observed in this dataset:

| Code | Description |
|---|---|
| `C504` | Breast — upper outer quadrant |
| `C509` | Breast — unspecified |
| `C61` | Prostate |
| `Z129` | Encounter for screening, unspecified |
| `C20` | Rectum |
| `C541` | Corpus uteri |
| `C56` | Ovary |
| `C64` | Kidney |
| `C435` / `C437` | Malignant melanoma |
| `D391` | Neoplasm of uncertain behaviour — ovary |

---

### 6.7 `<pTNM>` — pathological TNM staging

Free text typed by pathologists, tissue only, optional. No format is enforced. Encodes tumour (T), node (N), and metastasis (M) classification at time of surgery. The M component is systematically absent in most records — pathologists enter what is known at the time and the system accepts anything.

Common patterns observed:

| Pattern | Example | Notes |
|---|---|---|
| No M detail | `T1NM` | Most common — M stage not yet determined |
| All unknown | `TNM` | No staging available at export time |
| Full N detail | `T3N0M` | N known, M not recorded |
| With substage | `T1cN2M` | Substage on T component |
| With M1 | `T4aN2M1` | Metastatic disease confirmed |

Parsers should treat this as an opaque string and not attempt to parse T/N/M components reliably.

---

### 6.8 `<morphology>` — ICD-O-3 code

Optional, tissue only. Max 7 characters. Format: `{4-digit histology code}/{behaviour digit}`.

Behaviour digit meanings: `0` = benign, `1` = uncertain, `2` = in situ, `3` = malignant primary, `6` = malignant metastasis, `9` = malignant uncertain.

Examples from observed data: `8500/32` (infiltrating duct carcinoma, no grade), `8140/32` (adenocarcinoma), `8380/31` (endometrioid carcinoma), `8720/69` (melanoma).

---

### 6.9 `<samplesNo>` and `<availableSamplesNo>`

Both are integers, always present on tissue/serum/genome. `availableSamplesNo` ≤ `samplesNo`. The difference represents aliquots that have been consumed (e.g. sent to researchers). A value of `0` for `availableSamplesNo` means the sample is exhausted.

---

### 6.10 `<cutTime>` and `<freezeTime>` (tissue only)

Both accept either `xs:dateTime` (e.g. `2023-03-24T11:15:00`) or `xs:date` (e.g. `2023-03-24`). In practice datetime is almost always used. The gap between `cutTime` and `freezeTime` represents warm ischaemia time — typically 5–30 minutes.

---

### 6.11 `<takingDate>` (serum / genome / diagnosisMaterial)

Same union type as cutTime/freezeTime — accepts datetime or date. Serum and genome entries usually use date only. STS `diagnosisMaterial` usually uses datetime.

---

### 6.12 `<retrieved>` — collection context

| Value | Meaning | Seen on |
|---|---|---|
| `operational` | Sample taken during an active surgical/clinical procedure | tissue, serum, genome |
| `unknown` | Collection context not recorded | tissue, serum, genome, diagnosisMaterial |

In STS, `retrieved` is always `unknown`. For tissue it is usually `operational`. For serum/genome it is present ~60% of the time and can be either value.

---

## 7. Edge Cases and Notes

- **Self-closing patient stubs**: `consent="false"` produces `<patient ... />` with no children. Parsers must handle this — it is not an error.
- **STS sampleId `&amp;` prefix**: the XML entity `&amp;` decodes to `&`. A parsed sampleId looks like `&:2022:118485`. This is intentional — the `&` prefix distinguishes STS samples from LTS samples in the internal system.
- **`biopsy="-"` and `predictive_number="-"`**: the literal dash string means "not applicable". These attributes are always present on LTS samples but may be `-` when no pathology or sequencing request was linked.
- **Multiple LTS rows from one event**: a single surgery (same `number`) produces one `<tissue>` row per `materialType` — commonly `1` + `4` + `53` + `54` (tumour, healthy, and their RNAlater counterparts). Similarly a blood draw produces one `<serum>` row per fraction (`K` + `SD`).
- **`AccessionNumbers` inside LTS samples**: post-2024 files include `<AccessionNumbers/>` (empty) on every LTS sample even when no numbers exist. Pre-2024 files omit the element entirely. Parsers must tolerate both.
- **`month` is optional**: some patient records omit the `month` attribute entirely.
- **Patient birth year upper range**: years into the 2000s represent paediatric cases.
- **`diagnosisMaterial` can appear in LTS**: rare but valid — the schema permits it and it is observed in some files.
- **`availableSamplesNo` can be 0**: sample was collected but is fully consumed. Still appears in export.
- **`pTNM` free text**: no strict format enforced. Missing M component (`T1NM` instead of `T1N0M`) is common. Parsers should not assume a fixed structure.

---

## 8. Full Example Files

### Example 1 — Consent false, stub only

Patient declined consent. Self-closing element, no clinical data.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="false" id="271801" month="--07" sex="male"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1948"/>
```

---

### Example 2 — STS only, empty LTS

Patient had a diagnostic blood draw (STS). No biobank sample was archived.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="true" id="247" month="--02" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1957">
  <LTS/>
  <STS>
    <diagnosisMaterial number="118485" sampleId="&amp;:2022:118485" year="2022">
      <materialType>S</materialType>
      <diagnosis>C504</diagnosis>
      <takingDate>2022-09-20T10:44:00</takingDate>
      <retrieved>unknown</retrieved>
    </diagnosisMaterial>
  </STS>
</patient>
```

---

### Example 3 — LTS serum only (simple, no biopsy link)

Patient donated a serum sample. No concurrent tissue or pathology biopsy.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="true" id="138423" month="--05" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1943">
  <LTS>
    <serum biopsy="-" number="3249" predictive_number="-" sampleId="BBMs:2022:3249:SD" year="2022">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>SD</materialType>
      <takingDate>2022-12-07</takingDate>
    </serum>
  </LTS>
  <STS>
    <diagnosisMaterial number="155548" sampleId="&amp;:2022:155548" year="2022">
      <materialType>S</materialType>
      <diagnosis>Z129</diagnosis>
      <takingDate>2022-12-07T07:35:00</takingDate>
      <retrieved>unknown</retrieved>
    </diagnosisMaterial>
  </STS>
</patient>
```

---

### Example 4 — LTS genome only (PS material, with AccessionNumbers)

Patient donated whole blood with DNA stabiliser. Has prior radiology history.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="true" id="170096" month="--09" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1965">
  <AccessionNumbers>
    <Number>RDG2004019834</Number>
  </AccessionNumbers>
  <LTS>
    <genome biopsy="-" number="1075" predictive_number="-" sampleId="BBMd:2025:1075:PS" year="2025">
      <AccessionNumbers/>
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>PS</materialType>
      <takingDate>2025-07-29</takingDate>
      <retrieved>unknown</retrieved>
    </genome>
  </LTS>
</patient>
```

---

### Example 5 — LTS tissue + serum with biopsy and predictive_number, pTNM, morphology

Surgery patient. One biopsy event (`2023/2872`) produced tissue and serum samples. Two different diagnoses on the serum (multi-focal). Tissue has TNM staging and morphology. Genome sample (whole blood) taken at same visit with no biopsy link.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="true" id="463988" month="--10" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1977">
  <LTS>
    <genome biopsy="-" number="249" predictive_number="-" sampleId="BBMd:2023:249:PK" year="2023">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>PK</materialType>
      <takingDate>2023-03-24</takingDate>
      <retrieved>unknown</retrieved>
    </genome>
    <serum biopsy="2023/2872-1" number="524" predictive_number="2023/1052" sampleId="BBMs:2023:524:K" year="2023">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>K</materialType>
      <diagnosis>C56</diagnosis>
      <takingDate>2023-03-24</takingDate>
    </serum>
    <serum biopsy="2023/2872-2" number="524" predictive_number="2023/1052" sampleId="BBMs:2023:524:K" year="2023">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>K</materialType>
      <diagnosis>C541</diagnosis>
      <takingDate>2023-03-24</takingDate>
    </serum>
    <serum biopsy="2023/2872-1" number="524" predictive_number="2023/1052" sampleId="BBMs:2023:524:SD" year="2023">
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>SD</materialType>
      <diagnosis>C56</diagnosis>
      <takingDate>2023-03-24</takingDate>
    </serum>
    <serum biopsy="2023/2872-2" number="524" predictive_number="2023/1052" sampleId="BBMs:2023:524:SD" year="2023">
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>SD</materialType>
      <diagnosis>C541</diagnosis>
      <takingDate>2023-03-24</takingDate>
    </serum>
    <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052" sampleId="BBM:2023:181:1" year="2023">
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>1</materialType>
      <pTNM>T1N0M</pTNM>
      <morphology>8380/31</morphology>
      <diagnosis>C56</diagnosis>
      <cutTime>2023-03-24T11:15:00</cutTime>
      <freezeTime>2023-03-24T11:20:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
    <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052" sampleId="BBM:2023:181:4" year="2023">
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>4</materialType>
      <pTNM>T1N0M</pTNM>
      <morphology>8380/31</morphology>
      <diagnosis>C56</diagnosis>
      <cutTime>2023-03-24T11:15:00</cutTime>
      <freezeTime>2023-03-24T11:20:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
    <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052" sampleId="BBM:2023:181:53" year="2023">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>53</materialType>
      <pTNM>T1N0M</pTNM>
      <morphology>8380/31</morphology>
      <diagnosis>C56</diagnosis>
      <cutTime>2023-03-24T11:15:00</cutTime>
      <freezeTime>2023-03-24T11:20:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
    <tissue biopsy="2023/2872-1" number="181" predictive_number="2023/1052" sampleId="BBM:2023:181:54" year="2023">
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>54</materialType>
      <pTNM>T1N0M</pTNM>
      <morphology>8380/31</morphology>
      <diagnosis>C56</diagnosis>
      <cutTime>2023-03-24T11:15:00</cutTime>
      <freezeTime>2023-03-24T11:20:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
  </LTS>
  <STS>
    <diagnosisMaterial number="40063" sampleId="&amp;:2023:40063" year="2023">
      <materialType>S</materialType>
      <diagnosis>D391</diagnosis>
      <takingDate>2023-03-22T12:16:00</takingDate>
      <retrieved>unknown</retrieved>
    </diagnosisMaterial>
  </STS>
</patient>
```

---

### Example 6 — AccessionNumbers + genome + serum + tissue, no STS

Patient with long radiology history. Tissue (kidney tumour, type `1` + RNAlater `53`), serum (K + SD), genome (PK). No STS record.

```xml
<?xml version="1.0" encoding="utf-8" ?>
<patient biobank="MOU" consent="true" id="173254" month="--09" sex="female"
  xmlns="http://www.bbmri.cz/schemas/biobank/data"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:noNamespaceSchemaLocation="exportNIS.xsd" year="1945">
  <AccessionNumbers>
    <Number>RDG2005041156</Number>
    <Number>RDG2006036054</Number>
    <Number>RDG2008045182</Number>
    <Number>RDG2009008864</Number>
    <Number>RDG2011016213</Number>
    <Number>RDG2013029033</Number>
    <Number>RDG2015046988</Number>
    <Number>RDG2017046012</Number>
    <Number>RDG2019048259</Number>
    <Number>RDG2021047058</Number>
  </AccessionNumbers>
  <LTS>
    <genome biopsy="-" number="372" predictive_number="-" sampleId="BBMd:2024:372:PK" year="2024">
      <AccessionNumbers/>
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>PK</materialType>
      <takingDate>2024-05-16</takingDate>
      <retrieved>operational</retrieved>
    </genome>
    <serum biopsy="2024/4776-1" number="452" predictive_number="-" sampleId="BBMs:2024:452:K" year="2024">
      <AccessionNumbers/>
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>K</materialType>
      <diagnosis>C64</diagnosis>
      <takingDate>2024-05-16</takingDate>
    </serum>
    <serum biopsy="2024/4776-1" number="452" predictive_number="-" sampleId="BBMs:2024:452:SD" year="2024">
      <AccessionNumbers/>
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>SD</materialType>
      <diagnosis>C64</diagnosis>
      <takingDate>2024-05-16</takingDate>
    </serum>
    <tissue biopsy="2024/4776-1" number="317" predictive_number="-" sampleId="BBM:2024:317:1" year="2024">
      <AccessionNumbers/>
      <samplesNo>3</samplesNo>
      <availableSamplesNo>3</availableSamplesNo>
      <materialType>1</materialType>
      <pTNM>T1NM</pTNM>
      <morphology>8310/31</morphology>
      <diagnosis>C64</diagnosis>
      <cutTime>2024-05-16T11:45:00</cutTime>
      <freezeTime>2024-05-16T11:56:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
    <tissue biopsy="2024/4776-1" number="317" predictive_number="-" sampleId="BBM:2024:317:53" year="2024">
      <AccessionNumbers/>
      <samplesNo>1</samplesNo>
      <availableSamplesNo>1</availableSamplesNo>
      <materialType>53</materialType>
      <pTNM>T1NM</pTNM>
      <morphology>8310/31</morphology>
      <diagnosis>C64</diagnosis>
      <cutTime>2024-05-16T11:45:00</cutTime>
      <freezeTime>2024-05-16T11:56:00</freezeTime>
      <retrieved>operational</retrieved>
    </tissue>
  </LTS>
</patient>
```

