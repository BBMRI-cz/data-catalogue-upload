# The data catalogue's API contract

What the uploader talks to, and exactly how. Everything below was verified against the live
instance on 2026-09-08; the transcript in §9 is real output, not an example.

## 1. What the catalogue is

| | |
|---|---|
| Product | **MOLGENIS EMX2** |
| Specification version | `v13.120.1` |
| Implementation | `73dbdaa8c`, database version `32` |
| Host | `https://data.bbmri.cz` |
| Test schema | `FairGenomesTest` |
| Production schema | `FairGenomes` — **still carries the FAIR Genomes v1.3 model**, not v2 (see §8) |
| GraphQL endpoint | `POST {host}/{schema}/api/graphql` |
| Bulk endpoints | `{host}/{schema}/api/csv/{Table}`, `/api/zip`, `/api/excel` |

Version is readable without credentials:

```
POST https://data.bbmri.cz/api/graphql
{"query":"{_manifest{ImplementationVersion,SpecificationVersion,DatabaseVersion}}"}
```

`FairGenomesTest` implements [fair-genomes-v2.yml](fair-genomes-v2.yml) — 80 tables: 19 `DATA`
tables (6 of which inherit from `ImagingSeries`) and 61 `ONTOLOGIES` lookup tables.

## 2. Authentication

An EMX2 API token in the **`x-molgenis-token`** header on every request:

```
POST https://data.bbmri.cz/FairGenomesTest/api/graphql
Content-Type: application/json
x-molgenis-token: <jwt>
```

Tokens are minted in the EMX2 UI (account menu, Manage tokens) or by
`mutation{signin(email:"...",password:"..."){status message token}}` against `{host}/api/graphql`.
Check who a token is with `{_session{email,roles,admin}}`.

The token lives in `.env` (git-ignored) as `CATALOGUE_TOKEN`; `.env.example` carries the key with
an empty value. Nothing authenticates by cookie, and no credential belongs in the repository.

> **Scope the token to the schema it writes.** `Editor` on the target schema is everything the
> uploader needs. An admin-scoped token reaches every schema on the host, which is far more than a
> scheduled job should hold, so do not use one for a deployed run.
>
> Tokens are signed with a key the server holds. If the instance is restarted or rekeyed, every
> previously issued token stops parsing — the symptom is `Cannot parse token` on a request that
> worked yesterday, with no change on our side. Mint a new one and carry on.

## 3. Naming: three conventions, do not mix them

The single easiest thing to get wrong.

| Where | Convention | Example |
|---|---|---|
| GraphQL **query fields** and **mutation input fields** | **camelCase** | `personalIdentifier`, `belongsToPerson`, `materialType` |
| GraphQL **mutation argument** and **input type name** | PascalCase table id | `save(Personal: [PersonalInput])` |
| `_schema` metadata, CSV/Excel headers | PascalCase | `PersonalIdentifier`, `BelongsToPerson` |

Both derive from the YAML by removing spaces and punctuation: `Sampleprep identifier` becomes
`SampleprepIdentifier` / `sampleprepIdentifier`; `Percentage Q30` becomes `PercentageQ30` /
`percentageQ30`; `Whole slide imaging (WSI)` becomes `WholeSlideImagingWsi`.

**The uploader therefore serializes payloads with `JsonNamingPolicy.CamelCase`** and reads results
the same way. (The older public `FairGenomes` schema uses an all-lowercase convention instead —
`personalidentifier`. Do not take naming from it.)

## 4. Identifiers: the catalogue assigns none

Every `DATA` table's primary key is the `UniqueID` element from the YAML: `key = 1`,
`required = true`, type `STRING`. **We supply it.** A mutation returns only
`{taskId, status, message}` — there is no generated id to read back.

For the uploader that means "the identifier the catalogue assigned" is just the pseudonym we sent,
and `SyncState.CatalogueRemoteId` records it. A later run finds the same row by sending the same key.

References are **nested key objects**, never bare strings:

```jsonc
"belongsToPerson":     { "personalIdentifier": "mmci_patient_<uuid>" },   // REF
"belongsToDiagnosis": [{ "clinicalIdentifier": "mmci_clinical_<uuid>" }], // REF_ARRAY
"materialType":        { "name": "Frozen Tissue" }                        // ONTOLOGY
```

`ONTOLOGY` and `ONTOLOGY_ARRAY` columns reference a lookup table whose key column is **`name`**.
Dates and datetimes are `String` scalars — send ISO-8601.

## 5. Create, update, delete

| Operation | Mutation |
|---|---|
| Create **or** update | `save` — a true upsert, keyed on the primary key |
| Create only | `insert` — fails if the key exists |
| Update only | `update` — fails if the key does not exist |
| Delete | `delete` — takes rows carrying just the key |

Use `save`. There is no create-then-amend dance.

**`save` replaces the whole row; it does not merge.** Transcript step 3 sends a row without
`genderAtBirth` and the previously stored value is gone, with `mg_insertedOn` moving too. Every
`save` must carry the complete row as it should end up.

**Every mutation is transactional and validated.** One bad value fails the entire call and writes
nothing (transcript step 4).

### Deleting does not cascade

EMX2 refuses to delete a row that another row still references (transcript step 6):

```
Delete into table Personal failed: Transaction failed: update or delete on table "Personal"
violates foreign key constraint "Clinical.BelongsToPerson REFERENCES Personal" on table
"Clinical". Details: Key (PersonalIdentifier)=(contract82_person_1) is still referenced from
table "Clinical".
```

So a delete must walk the graph child-first. For the tables the uploader writes:

```
Analysis -> Sequencing -> SamplePreparation -> Biospecimens -> Material
         -> Clinical -> IndividualConsent -> Personal -> Study
```

The upside: an orphan is impossible to create by accident. Get the order wrong and the call fails
loudly instead of silently leaving dangling rows behind.

## 6. The tables the uploader writes

Write order is the reverse of the delete order — parents first. Column names below are the
**camelCase GraphQL input** names. An arrow marks a reference and names the table it points at.

| Table | Key | Other columns |
|---|---|---|
| `Study` | `identifier` | `name`, `description`, `principalInvestigator`, `contactInformation`, `studyDesign`, `startDate`, `completionDate`; ontology `inclusionCriteria` |
| `Personal` | `personalIdentifier` | `yearOfBirth`, `ageAtDeath`, `participatesInStudy` -> *Study*; ontologies `genderAtBirth`, `countryOfResidence`, `countryOfBirth`, `ancestry`, `status`, `primaryAffiliatedInstitute` |
| `IndividualConsent` | `individualConsentIdentifier` | `personConsenting` -> *Personal*, `belongsToStudy` -> *Study*, `consentFormUsed` -> *LeafletAndConsentForm*, `signingDate`, `validUntil`; ontologies `collectedBy`, `representedBy`, `dataUsePermissions`, `dataUseModifiers`, `allowRecontacting` |
| `Clinical` | `clinicalIdentifier` | `belongsToPerson` -> *Personal*, `ageAtDiagnosis`; ontologies `diagnosis`, `clinicalTimepoint`, `diseaseStage`, `molecularDiagnosisGene`, `treatmentCategory`, `responseToTreatment` |
| `Material` | `materialIdentifier` | `collectedFromPerson` -> *Personal*, `belongsToDiagnosis` -> *Clinical* (array), `samplingDate`; ontologies `materialType`, `anatomicalSource`, `pathologicalState` |
| `Biospecimens` | `biospecimenIdentifier` | `derivedFromMaterial` -> *Material*, `percentageTumorCells` (decimal), `quantity` (int); ontologies `biospecimenForm`, `storageConditions`, `managingBiobank`, `availability`, `nameOfFixative`, `embeddingMedium` |
| `SamplePreparation` | `sampleprepIdentifier` | `belongsToBiospecimen` -> *Biospecimens*, `inputAmount`, `pcrFree`, `umisPresent`, `intendedInsertSize`, `intendedReadLength`; ontologies `libraryPreparationKit`, `targetEnrichmentKit`, `fullySequencedGenes`, `partiallySequencedGenes` |
| `Sequencing` | `sequencingIdentifier` | `belongsToSamplePreparation` -> *SamplePreparation*, `sequencingDate`, `medianReadDepth`, `observedReadLength`, `observedInsertSize`, `percentageQ30`, `percentageTr20`, `otherQualityMetrics`; ontologies `sequencingPlatform`, `sequencingInstrumentModel`, `sequencingMethod` |
| `Analysis` | `analysisIdentifier` | `belongsToSequencing` -> *Sequencing*, `algorithmsUsed`, `bioinformaticProtocolUsed`; ontologies `dataFormatsStored`, `referenceGenomeUsed` |

Not written, because no source service feeds them: `ImagingStudy`, `ImagingSeries` and its six
modality subclasses, `SlidePreparationAssay`, `LeafletAndConsentForm`.

Every table also exposes `mg_draft`, `mg_insertedBy/On`, `mg_updatedBy/On` and `REFBACK` columns.
Write none of them — a `REFBACK` is the inverse of someone else's reference and EMX2 maintains it.

## 7. Lookup tables are strict

An `ONTOLOGY` column is a plain foreign key. An unknown term **rejects the whole row** and is not
auto-created (transcript step 4). The uploader therefore reads each referenced ontology's `name`
column once per run and omits any term it cannot match.

Contents as loaded in `FairGenomesTest`:

| Table | Terms | Coding system | Usable from our sources? |
|---|---|---|---|
| `PathologicalState` | 20 | NCIT — only `Normal`, `Organoid`, `Tumor`, `Tumoroid` are real | yes |
| `MaterialType`, `BiospecimenForm` | 419 | NCIT (`Frozen Tissue`, `Peripheral Blood`, ...) | yes, by hand-written crosswalk |
| `GenderAtBirth` | 29 | GSSO (`assigned male at birth`, ...) | yes |
| `Status`, `Availability` | 20 each | `Alive`/`Dead`/..., `Available`/`Reserved`/... | where known |
| `SequencingPlatform` | 23 | `Illumina platform`, `PacBio platform`, ... | yes |
| `SequencingInstrumentModel` | 61 | | yes |
| `SequencingMethod` | 51 | | yes |
| `ReferenceGenomeUsed` | 45 | `GRCh37`, `GRCh38`, `.pN` variants | yes |
| `DataFormatsStored` | 598 | EDAM (`FASTQ`, `BAM`, `VCF`, ...) | yes |
| `FullySequencedGenes`, `MolecularDiagnosisGene` | 19218 | HGNC symbols | yes, panel gene symbols |
| `AnatomicalSource` | 13843 | UBERON/NCIT | no source field today |
| `ManagingBiobank`, `PrimaryAffiliatedInstitute` | 244 | institute list | check MMCI is present |
| **`Diagnosis`** | 22042 | **Orphanet only** | **no — see below** |

Every ontology also carries the HL7 nullflavor terms (`Unknown (UNK, nullflavor)`,
`Not applicable (NA, nullflavor)`, ...) from the schema's `lookupGlobalOptions`, giving a legal way
to state "not known" rather than omitting the column.

### Open question: `Diagnosis` is Orphanet, our data is ICD-10

`Diagnosis` holds 22042 Orphanet rare-disease terms (`Alexander disease`, code `58`, codesystem
`Orphanet`). The biobank serves ICD-10 oncology codes, and no usable ICD-10 to Orphanet crosswalk
exists for common tumours. The YAML anticipated other coding systems — it chose `EDAM:data_3667`
over `data_2800` explicitly *"to allow alternative disease coding systems (e.g. ICD or SNOMED)"* —
but only Orphanet terms are loaded.

**Until the schema owner loads ICD-10 into `Diagnosis`, `Clinical.diagnosis` is left empty.** This
is the one field the catalogue cannot currently receive from us.

## 8. The production schema is a different model

`FairGenomes` (production) is FAIR Genomes **v1.3**: no `Biospecimens`, no `Imaging series`, and it
still has `Fixed block`, `Slide container` and `Treatment`, which v2 drops. Its columns are
all-lowercase (`personalidentifier`). Uploading there needs the v2 model deployed first — a schema
owner's job, not the uploader's.

## 9. Transcript

Verbatim, against `https://data.bbmri.cz/FairGenomesTest/api/graphql` on 2026-09-08. Every request
carried `x-molgenis-token`. The schema had 0 rows before and 0 rows after.

### 1. Create

```graphql
mutation Save($rows:[PersonalInput]){ save(Personal:$rows){ status message } }
```
```json
{"rows":[{"personalIdentifier":"contract82_person_1","yearOfBirth":1970,
          "genderAtBirth":{"name":"assigned female at birth"}}]}
```
```json
{"data":{"save":{"status":"SUCCESS","message":"upserted 1 records to Personal\n"}}}
```

### 2. Read back

```graphql
{ Personal(filter:{personalIdentifier:{equals:"contract82_person_1"}}){
    personalIdentifier yearOfBirth genderAtBirth{name} mg_insertedOn mg_updatedOn } }
```
```json
{"data":{"Personal":[{"personalIdentifier":"contract82_person_1","yearOfBirth":1970,
  "genderAtBirth":{"name":"assigned female at birth"},
  "mg_insertedOn":"2026-09-08T12:31:25.502125","mg_updatedOn":"2026-09-08T12:31:25.502125"}]}}
```

Query fields are camelCase: `PersonalIdentifier` is rejected with
`Field 'PersonalIdentifier' in type 'Personal' is undefined`.

### 3. Update through the same `save`, and the replace semantics

```json
{"rows":[{"personalIdentifier":"contract82_person_1","yearOfBirth":1971}]}
```
```json
{"data":{"save":{"status":"SUCCESS","message":"upserted 1 records to Personal\n"}}}
```

Reading back:

```json
{"data":{"Personal":[{"personalIdentifier":"contract82_person_1","yearOfBirth":1971,
  "mg_insertedOn":"2026-09-08T12:31:34.903005","mg_updatedOn":"2026-09-08T12:31:34.903005"}]}}
```

`yearOfBirth` updated — and `genderAtBirth` is **gone**, because the row was replaced rather than
merged. `mg_insertedOn` moved too.

### 4. An unknown ontology term rejects the row

```json
{"rows":[{"personalIdentifier":"contract82_person_1","yearOfBirth":1971,
          "genderAtBirth":{"name":"C50.9 zhoubny novotvar prsu"}}]}
```
```json
{"errors":[{"message":"Upsert into table 'Personal' failed: Transaction failed: insert or update
on table \"Personal\" violates foreign key constraint \"Personal.GenderAtBirth REFERENCES
GenderAtBirth\". Details: Key (GenderAtBirth)=(C50.9 zhoubny novotvar prsu) is not present in
table \"GenderAtBirth\"."}]}
```

`GenderAtBirth_agg{count}` stayed at 29 — nothing was auto-created.

### 5. Create a referencing child

```graphql
mutation Save($rows:[ClinicalInput]){ save(Clinical:$rows){ status message } }
```
```json
{"rows":[{"clinicalIdentifier":"contract82_clinical_1",
          "belongsToPerson":{"personalIdentifier":"contract82_person_1"},"ageAtDiagnosis":55}]}
```
```json
{"data":{"save":{"status":"SUCCESS","message":"upserted 1 records to Clinical\n"}}}
```

### 6. Deleting the referenced parent fails

```graphql
mutation Del($rows:[PersonalInput]){ delete(Personal:$rows){ status message } }
```
```json
{"errors":[{"message":"Delete into table Personal failed: Transaction failed: update or delete on
table \"Personal\" violates foreign key constraint \"Clinical.BelongsToPerson REFERENCES
Personal\" on table \"Clinical\". Details: Key (PersonalIdentifier)=(contract82_person_1) is
still referenced from table \"Clinical\"."}]}
```

### 7. Child first, then parent

```json
{"data":{"delete":{"status":"SUCCESS","message":"delete 1 records from Clinical\n"}}}
{"data":{"delete":{"status":"SUCCESS","message":"delete 1 records from Personal\n"}}}
```

### 8. Left as found

```json
{"data":{"Personal_agg":{"count":0},"Clinical_agg":{"count":0},"Material_agg":{"count":0},
         "Biospecimens_agg":{"count":0},"Study_agg":{"count":0}}}
```
