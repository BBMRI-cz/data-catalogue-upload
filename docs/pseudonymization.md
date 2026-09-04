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

### Personal

| field | FAIR type | published value |
|---|---|---|
| `PersonalIdentifier` | UniqueID | `mmci_patient_<uuid>` — minted |

### Clinical

| field | FAIR type | published value |
|---|---|---|
| `ClinicalIdentifier` | UniqueID | `mmci_clinical_<uuid>` — derived from the patient pseudonym |
| `BelongsToPerson` | → Personal | `mmci_patient_<uuid>` |

### Material

| field | FAIR type | published value |
|---|---|---|
| `MaterialIdentifier` | UniqueID | `mmci_sample_<uuid>` — minted |
| `CollectedFromPerson` | → Personal | `mmci_patient_<uuid>` |
| `BelongsToDiagnosis` | → Clinical | `mmci_clinical_<uuid>` — derived the same way as the key it points at |
| `DerivedFrom` | String, not a reference | **dropped** — see the decisions below |

### SamplePreparation

| field | FAIR type | published value |
|---|---|---|
| `SampleprepIdentifier` | UniqueID | `mmci_sampleprep_<uuid>_<run>` — from the source |
| `BelongsToMaterial` | → Material | `mmci_sample_<uuid>` |

### Sequencing

| field | FAIR type | published value |
|---|---|---|
| `SequencingIdentifier` | UniqueID | `mmci_predictive_<uuid>_<run>` — from the source |
| `BelongsToSamplePreparation` | → SamplePreparation | `mmci_sampleprep_<uuid>_<run>` |

### Analysis

| field | FAIR type | published value |
|---|---|---|
| `AnalysisIdentifier` | UniqueID | `mmci_analysis_<uuid>_<run>` — from the source |
| `BelongsToSequencing` | → Sequencing | `mmci_predictive_<uuid>_<run>` |
| `AbstractDataLocation` | String | paths under `Samples/mmci_predictive_<uuid>/` — already pseudonymous |

## Decisions

**Only two identifiers are minted.** Everything else either derives from them or arrives
pseudonymous. `BiobankMapping.ClinicalIdentifier`, handed `mmci_patient_<uuid>`, answers
`mmci_clinical_<uuid>` — the same helper the inbound mapper uses, called with a pseudonym instead of
a real id. It was written for this.

**The sequencing chain needs no work.** `SequencingMapping` derives its three identifiers from the
sequencing API's `samples[].sample_id`, which *is* the run tree's `mmci_predictive_<uuid>` folder
name — renamed in place by the pseudonymizer before the data left for SensitiveCloud.
`Analysis.AbstractDataLocation` is built from those same paths, so it is pseudonymous for the same
reason.

**The pseudonymizer's mapping files are never read.** `predictive.json` maps pseudonym → real, which
is the sequencing API's job, not ours. `patients.json` and `samples.json` cover only the sequenced
subset of the biobank and nothing downstream references them. The mount is never opened, so it
cannot be written to.

**A reference is derived, never copied.** A FAIR Genomes reference stores the referenced row's
`UniqueID` value, so a reference that stops matching the key it points at breaks the catalogue's
graph without anything failing. `Material.BelongsToDiagnosis` is therefore produced by the same
call that produces `Clinical.ClinicalIdentifier`, not copied from the domain.

**`Material.DerivedFrom` is dropped rather than forwarded.** It references a *different* sample's
material, so neither this sample's pseudonym nor the real id is the right answer. Nothing sets it
today; whoever wires it up has to resolve it deliberately.
