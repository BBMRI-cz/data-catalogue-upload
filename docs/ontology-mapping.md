# Ontology mapping

How the uploader turns a source value into an ontology term, and where the ontology-mapping
student's approved table plugs in. Specified in
[#116](https://github.com/BBMRI-cz/data-catalogue-upload/issues/116); the patient's fields went first
([#119](https://github.com/BBMRI-cz/data-catalogue-upload/issues/119)), then the sample's
([#120](https://github.com/BBMRI-cz/data-catalogue-upload/issues/120)).

**The rule: every ontology value is resolved while its payload is built, before the payload is
fingerprinted.** An EMX2 ontology column is a foreign key, so a value that is not already a term
rejects the whole row (see [`catalogue-api-contract.md`](catalogue-api-contract.md)). Leaving that to
the gateway's check on the way out meant fingerprinting a value the catalogue never received: when a
mapping for it arrived later, the fingerprint had not changed and the row was skipped for good.
Resolving first means a mapping that starts answering changes the row, the fingerprint, and so what
the next run re-sends.

## The resolver

`TermResolver` (`Uploader.Application/Mapping/Ontology/`, with `OntologyFields`, `UnmappedValue` and the stand-in) is built once per run, from that run's term lists
and mapping-table snapshot, and handed to the payload mappers. It does no I/O. Each value reaches it
exactly as the source served it (`C504`, `Female`, `MOU`, `53`), and goes through three steps:

1. A value that is already an exact term of the field's live list is used as is. `C50` or an already
   correct institute name needs no mapping entry.
2. Otherwise the mapping table is asked about the value in its context. A term it answers is used
   only if that term is itself in the field's live list; any other answer is treated as unmapped. A
   "no term" answer leaves the field out on purpose.
3. Anything else leaves the field out and records the value as **unmapped**.

A field left out does not cost the row: the rest of the row is still sent. A value the source did not
fill (null or blank) is simply absent, not unmapped. An array field (`Clinical.diagnosis`) is resolved
element by element, then de-duplicated and ordered, so `C504` and `C50.4` are one entry and the
fingerprint does not follow the source's order. A patient with nothing to publish there still sends
an empty list rather than none: the gateway leaves an empty ontology array out of the row anyway, so
only the fingerprint could tell the two apart, and changing it would re-send every such patient for
nothing.

## The three answers

The mapping table (`IMappingTable`, `Uploader.Application/Abstractions/`) answers a lookup by field,
value and context in one of three ways:

| answer | the field | the unmapped list |
|---|---|---|
| a term (`MappingAnswer.Mapped`) that is live | filled with the term | - |
| a term that is not live | left out | recorded |
| "no term" (`MappingAnswer.NoTerm`) | left out | - |
| not found (`MappingAnswer.NotFound`) | left out | recorded |

"No term" and not found look the same in the catalogue. The difference is that "no term" is a
decision - the value has no term on purpose - while not found is a value nobody has decided on yet,
which is what the unmapped list exists to surface.

The table is loaded once per run (`LoadAsync`) into a snapshot whose lookups are synchronous and in
memory, so a sync never waits on whoever maintains the table.

## Run start

A run upserts the study, then reads the live term list of **every** ontology field the uploader fills
(`OntologyFields.All`, 18 of them) through `ICatalogueGateway.ReadTermListsAsync`, then loads the
mapping table, and only then starts on the patients. The term lists come from the gateway's existing
schema reader (`Emx2Schema`), so the resolver and the gateway's own term check share one read per run.

If the lists cannot be read, a field is not an ontology column in the live schema, or the mapping
table cannot be loaded, the run ends before syncing any patient and records why in `sync_run`'s
`failure`, as an unpublishable study does. Going on would be worse than stopping: resolving against a
list that is not there leaves every value of that field out, and a `save` replaces the whole row, so
the run would erase the terms already published.

## The fields

A field is a catalogue table and column as EMX2 spells them in a GraphQL mutation, for example
`Clinical.diagnosis`. The resolver fills these:

| field | raw value | from | context |
|---|---|---|---|
| `Personal.genderAtBirth` | the sex (`male`, `female`) | biobank `sex` | none |
| `Personal.primaryAffiliatedInstitute` | the biobank code (`MOU`, `BBM`) | biobank `biobank` | none |
| `Clinical.diagnosis` | each ICD-10 code without the dot (`C504`) | every non-genome sample's and every diagnostic specimen's `diagnosis` | none |
| `Material.materialType` | the material code (`1`, `53`, `SD`, `PK`) | sample `material_type` | `{sampleType, label}` |
| `Material.pathologicalState` | the material code | sample `material_type` | `{sampleType, label}` |
| `Biospecimens.biospecimenForm` | the material code | sample `material_type` | `{sampleType, label}` |
| `Biospecimens.storageConditions` | the material code | sample `material_type` | `{sampleType, label}` |
| `Biospecimens.managingBiobank` | the biobank code (`MOU`, `BBM`) | the patient's `biobank` | none |
| `Biospecimens.availability` | `Depleted` or `Available`, computed | sample `available_samples_no` | none |

**The material code's context** (`TermContext.Material`) is the biobank's sample type (`tissue`,
`serum`, `genome`) and the biobank API's `material_type_label` (`Malignant tumour (RNAlater)`, or
null for a code the API does not know). The same code is a different material on a different sample
type, and one code says something different in each of its four columns, so the lookup key is the
field, the value and the whole context. Today's rules read only the sample type; the label is there
for whoever maps a code without the biobank's code book to hand.

**Availability is computed, not mapped**: `Depleted` at zero units left, `Available` otherwise
(`CatalogueSampleMapper`). The export says only whether anything is left, so it is our code's to work
out rather than a translation, but the result still goes through the resolver's live-term check - a
catalogue that drops one of the two terms shows it as unmapped instead of rejecting the row.

The other fields in `OntologyFields.All` are read at run start but still translated by
`CatalogueVocabulary`, as before, and their misses are not reported yet: the sequencing's fields move
to the resolver in #121.

## The stand-in: `RuleMappingTable`

Until the student's approved table exists, `RuleMappingTable` stands in for it, and the snapshot it
hands out, `RuleMappingSnapshot`, holds the hand-written rules the uploader has always used, answering
through the same port. Loading does nothing: the rules are code, so every run gets the same snapshot.
It answers a term where a rule knows one, "no term" where leaving the field empty is a documented
decision, and not found everywhere else, so a value nobody has seen before shows up on the unmapped
list instead of vanishing.

| field | rule | not found |
|---|---|---|
| `Personal.genderAtBirth` | `male`/`female`, any case -> `assigned male at birth`/`assigned female at birth` (GSSO) | any other sex |
| `Personal.primaryAffiliatedInstitute` | `MOU`/`MMCI` -> `Masaryk Memorial Cancer Institute`, `BBM` -> `Bank of Biological Material, Masaryk Memorial Cancer Institute`, any case | an unknown biobank code |
| `Clinical.diagnosis` | the ICD-10 dot rule: trimmed, and a four-character code gets its dot (`C504` -> `C50.4`) | a code the rule hands back unchanged - already the ontology's spelling, or five characters with no rule for where the dot goes |

None of the patient's rules answers "no term". The sample's rules, by sample type and code (the label
is not read):

| field | term | "no term" | not found |
|---|---|---|---|
| `Material.materialType` | tissue -> `Solid Tissue Specimen`, except `7` (mononuclear cells) -> `Peripheral Blood`; serum and genome -> `Peripheral Blood` | serum `PR` (primary cell cultures are grown, not collected) | an unknown sample type |
| `Material.pathologicalState` | tissue `1`/`53`/`2`/`55` -> `Tumor`, `4`/`54` -> `Normal` | tissue `3`/`56` (benign), `5` (premalignant), `7` (mononuclear cells); every serum and genome code (a blood draw records no state) | any other tissue code; an unknown sample type |
| `Biospecimens.biospecimenForm` | tissue -> `Frozen Tissue`; serum -> `Serum or Plasma`; genome `gD` -> `Blood DNA`, other genome -> `Whole Blood` | tissue `7`, serum `PR` (not a listed form) | an unknown sample type |
| `Biospecimens.storageConditions` | tissue `1`-`5`/`7` and serum `C`/`K`/`L`/`PD`/`SD`/`T`/`PR` -> `Cryotube 1–2mL Programmable freezing to <-135°C`; genome `gD`/`PK` -> `PP tube 0.5–2mL (-35) to (-18)°C` | tissue `53`-`56` (RNAlater) | everything else - serum `S`/`PS`, genome `PS`, any new code |
| `Biospecimens.managingBiobank` | the institute list `Personal.primaryAffiliatedInstitute` uses | - | an unknown biobank code |

The "no term" column is the documented deliberate empties: the ontology has no honest term for them
(benign tissue is not "Tumor"), and leaving them out is a decision, not a gap. The not-found column is
where no rule has decided anything, so a new material code or biobank code shows up on the unmapped
list instead of vanishing as it used to. Genome `PS` (whole blood with DNA stabiliser) is the one
real code there today: the previous uploader named no tube for it, so every run reports its storage
condition once. The wildcard term rules (any tissue code is `Solid Tissue Specimen` and
`Frozen Tissue`, any serum code `Peripheral Blood` and `Serum or Plasma`) are the existing rules taken
over unchanged - a rule by sample type, so they do not report a new code; its storage condition, and
for tissue its pathological state, do.

## Unmapped values

An unmapped value is the field, the value as served, and its context - never an identifier or a
record. Values are collected per patient while that patient's payloads are built, and kept only if
the patient is upload-eligible: data of a patient without consent never leaves the uploader, not even
as an unmapped value. Each distinct value is logged once per run, as a warning that says whether the
mapping table had no answer or answered a term the live list does not have; a material code's warning
carries its context (`Biospecimens.storageConditions 'PS' (sampleType: genome, label: Whole blood with
DNA stabiliser)`), since the code alone does not say which material it is.

The list is not kept beyond the run's log yet. Persisting it and serving it to the student at
`GET /admin/ontology/unmapped`, with a count in the run summary, is #122.

## The gateway's safety net

`Emx2Schema` still drops an ontology value that is not a term on its way out, with a warning, so a
bad value costs one column rather than the row. What it catches today is the fields still translated
by `CatalogueVocabulary` - the sequencing's - which nothing checks against the live lists before it. A resolved field only
reaches it holding a live term.

It checks against the same lists the resolver was built from - one read per run, held until the run
ends - so it does not notice a term removed from the ontology during a run. The catalogue rejects that
row and the run counts it as failed; the next run reads fresh lists, leaves the value out, records it
as unmapped and sends the row.

## Switching to the student's table

1. Implement `IMappingTable` over the approved table, in `Uploader.Infrastructure`. `LoadAsync` reads
   the whole table once into its own snapshot, the way `RuleMappingTable` hands out
   `RuleMappingSnapshot`; the snapshot's `Lookup` answers a term, "no term" or not found for a field,
   value and context. It may only answer exact terms of the field's list, or "no term" - the resolver
   checks every term against the live list and treats anything else as unmapped.
2. Swap the one registration in `Uploader.Infrastructure/DependencyInjection.cs`
   (`services.AddSingleton<IMappingTable, RuleMappingTable>()`). Nothing else in the uploader changes.

**Run the coverage check before switching.** Take every distinct value the sources serve today for
each field and look it up in both tables. Wherever the stand-in answers a term and the new table does
not answer the same one, the switch changes or removes a published term, and every row carrying it is
re-sent. Each such difference must be deliberate. In particular, a value the stand-in maps and the new
table does not know at all would disappear from the catalogue, and a deliberate empty has to be
"no term" in the new table, not missing.

## Rollout

The first run after the patient's fields moved to the resolver re-sends, once, the patients carrying
any of the 26 five-character diagnosis codes, now without them. Published content does not change,
because the gateway already dropped those codes; from then on the stored fingerprint describes what
the catalogue holds.

Moving the sample's fields re-sends nothing: every rule and term is unchanged, so every sample
payload is too.
