# Pseudonymization

What the uploader publishes in place of the real identifiers.
Written for [#81](https://github.com/BBMRI-cz/data-catalogue-upload/issues/81).

**The rule: the local sync database keeps the real identifiers; nothing real crosses to the
catalogue.** Sync state and fingerprints stay keyed on the biobank's own ids — that database never
leaves the host, and keeping it real is what lets a later run recognise the same patient. The
substitution happens once, in `CatalogueMapper`, on the way out.

## The two minted pseudonyms

| kind | real value | pseudonym |
|---|---|---|
| patient | export XML `@id`, e.g. `271801` | `mmci_patient_<uuid4>` |
| sample | export XML `@sampleId`, e.g. `BBMs:2022:3249:SD` | `mmci_sample_<uuid4>` |

Minted on first sight, stored in the `pseudonym` table, returned again on every later run — which is
what makes a second run update a catalogue record instead of duplicating it. That table is also the
only way back, and it stays on the biobank's host.

Prefix is `PSEUDONYM_PREFIX`, default `mmci`. One deployment per biobank.

## The published tables

Formats below use the default `mmci` prefix. **Two different uuids are in play**: the uploader mints
the patient and sample ones, while the sequencing chain carries the pseudonymizer's, taken from the
run tree's folder name — changing `PSEUDONYM_PREFIX` does not touch those. `<run>` is the instrument
run id, which scopes a resequenced sample's records so the second run cannot claim the first's.

### Study

Configuration, not patient data, and the one table here that carries no pseudonym at all.

| field | type | published value |
|---|---|---|
| `identifier` | key | `CATALOGUE_STUDY_ID`, default `mmci_biobank` |

### Personal

| field | type | published value |
|---|---|---|
| `personalIdentifier` | key | `mmci_patient_<uuid>` — minted |
| `participatesInStudy` | → Study | `CATALOGUE_STUDY_ID` |

### IndividualConsent

| field | type | published value |
|---|---|---|
| `individualConsentIdentifier` | key | `mmci_consent_<uuid>` — derived from the patient pseudonym |
| `personConsenting` | → Personal | `mmci_patient_<uuid>` |
| `belongsToStudy` | → Study | `CATALOGUE_STUDY_ID` |

### Clinical

| field | type | published value |
|---|---|---|
| `clinicalIdentifier` | key | `mmci_clinical_<uuid>` — derived from the patient pseudonym |
| `belongsToPerson` | → Personal | `mmci_patient_<uuid>` |

### Material

| field | type | published value |
|---|---|---|
| `materialIdentifier` | key | `mmci_sample_<uuid>` — minted |
| `collectedFromPerson` | → Personal | `mmci_patient_<uuid>` |
| `belongsToDiagnosis` | → Clinical | `mmci_clinical_<uuid>` — derived the same way as the key it points at |

### Biospecimens

| field | type | published value |
|---|---|---|
| `biospecimenIdentifier` | key | `mmci_biospecimen_<uuid>` — derived from the sample pseudonym |
| `derivedFromMaterial` | → Material | `mmci_sample_<uuid>` |

### SamplePreparation

| field | type | published value |
|---|---|---|
| `sampleprepIdentifier` | key | `mmci_sampleprep_<uuid>_<run>` — from the source |
| `belongsToBiospecimen` | → Biospecimens | `mmci_biospecimen_<uuid>` — derived, not copied |

### Sequencing

| field | type | published value |
|---|---|---|
| `sequencingIdentifier` | key | `mmci_predictive_<uuid>_<run>` — from the source |
| `belongsToSamplePreparation` | → SamplePreparation | `mmci_sampleprep_<uuid>_<run>` |

### Analysis

| field | type | published value |
|---|---|---|
| `analysisIdentifier` | key | `mmci_analysis_<uuid>_<run>` — from the source |
| `belongsToSequencing` | → Sequencing | `mmci_predictive_<uuid>_<run>` |

## Decisions

**Only two identifiers are minted.** Everything else either derives from them or arrives
pseudonymous. `BiobankMapping.ClinicalIdentifier`, handed `mmci_patient_<uuid>`, answers
`mmci_clinical_<uuid>` — the same helper the inbound mapper uses, called with a pseudonym instead of
a real id. It was written for this.

**The sequencing chain needs no work.** `SequencingMapping` derives its three identifiers from the
sequencing API's `samples[].sample_id`, which *is* the run tree's `mmci_predictive_<uuid>` folder
name — renamed in place by the pseudonymizer before the data left for SensitiveCloud.

**The pseudonymizer's mapping files are never read.** `predictive.json` maps pseudonym → real, which
is the sequencing API's job, not ours. `patients.json` and `samples.json` cover only the sequenced
subset of the biobank and nothing downstream references them. The mount is never opened, so it
cannot be written to.

**A reference is derived, never copied.** A FAIR Genomes reference stores the referenced row's
`UniqueID` value, so a reference that stops matching the key it points at breaks the catalogue's
graph without anything failing. `Material.BelongsToDiagnosis` is therefore produced by the same
call that produces `Clinical.ClinicalIdentifier`, not copied from the domain.

**File paths are no longer published at all.** v1 put the analysis output paths in
`AbstractDataLocation`, and they were pseudonymous because they sat under
`Samples/mmci_predictive_<uuid>/`. v2 removed both data-location columns, so an analysis output now
survives only as the format it is stored in. One fewer place for a path to leak from.

**A delete re-derives the keys rather than storing them.** When a patient disappears from the export
there is no aggregate left to read identifiers off, only the sync state's real ids. The gateway
resolves the pseudonym again — the map is idempotent — and applies the same derivation rules, so a
delete addresses exactly the rows the upload published. Nothing is stored twice, and there is no
second copy to drift.
