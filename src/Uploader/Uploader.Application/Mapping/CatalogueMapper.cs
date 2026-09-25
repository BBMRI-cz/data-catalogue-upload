using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the domain onto the EMX2 rows the catalogue receives, substituting a pseudonym for every
/// real identifier on the way out. This is the only place the two meet: aggregates, sync state and
/// fingerprints all stay keyed on the real ids, which is what lets a later run recognise the same
/// patient, and nothing real is written past this boundary.
/// <para>
/// Only two pseudonyms are minted. Every other identifier is derived from one of them - the
/// clinical, consent and biospecimen rows all take their key from the patient's or the sample's
/// pseudonym via <see cref="BiobankMapping"/> - and the sequencing chain arrives pseudonymized
/// already, its identifiers coming from the run tree's <c>mmci_predictive_&lt;uuid&gt;</c> folder
/// name. A reference is always re-derived, never copied, so it cannot drift from the key it points
/// at.
/// </para>
/// </summary>
public static class CatalogueMapper
{
    public static CataloguePatientPayload ToPayload(
        PatientAggregate patient,
        string patientPseudonym,
        string studyIdentifier) =>
        new()
        {
            ExternalId = patientPseudonym,
            Personal = ToRecord(patient.Personal, patientPseudonym, studyIdentifier),
            Consent = ToConsentRecord(patient, patientPseudonym, studyIdentifier),
            Clinical = ToRecord(patient.Clinical, patientPseudonym),
        };

    public static CatalogueSamplePayload ToPayload(
        SampleAggregate sample,
        string samplePseudonym,
        string patientPseudonym) =>
        new()
        {
            ExternalId = samplePseudonym,
            PatientId = patientPseudonym,
            Material = ToRecord(sample.Material, samplePseudonym, patientPseudonym),
            Biospecimen = ToRecord(sample.Biospecimen, samplePseudonym),
        };

    public static CatalogueSequencingPayload ToPayload(SequencingAggregate sequencing, string samplePseudonym) =>
        new()
        {
            ExternalId = samplePseudonym,
            SampleId = samplePseudonym,
            SamplePreparations =
                [.. sequencing.Preparations.Select(preparation => ToRecord(preparation, samplePseudonym))],
        };

    private static PersonalRecord? ToRecord(Personal? personal, string patientPseudonym, string studyIdentifier) =>
        personal is null
            ? null
            : new PersonalRecord
            {
                PersonalIdentifier = patientPseudonym,
                GenderAtBirth = personal.GenderAtBirth,
                CountryOfResidence = personal.CountryOfResidence,
                Ancestry = personal.Ancestry,
                CountryOfBirth = personal.CountryOfBirth,
                YearOfBirth = personal.YearOfBirth,
                Status = personal.Status,
                AgeAtDeath = personal.AgeAtDeath,
                PrimaryAffiliatedInstitute = personal.PrimaryAffiliatedInstitute,

                // Not source data: the one study this deployment publishes under.
                ParticipatesInStudy = [studyIdentifier],
            };

    /// <summary>
    /// A consent row for a patient who consented, and nothing for one who did not - an absent row
    /// is the honest record of an absent consent, and a patient without consent never reaches the
    /// catalogue anyway.
    /// </summary>
    private static IndividualConsentRecord? ToConsentRecord(
        PatientAggregate patient,
        string patientPseudonym,
        string studyIdentifier) =>
        patient.HasConsent
            ? new IndividualConsentRecord
            {
                IndividualConsentIdentifier = BiobankMapping.ConsentIdentifier(patientPseudonym),
                PersonConsenting = patientPseudonym,
                BelongsToStudy = studyIdentifier,
            }
            : null;

    private static ClinicalRecord? ToRecord(Clinical? clinical, string patientPseudonym) =>
        clinical is null
            ? null
            : new ClinicalRecord
            {
                // The same helper the inbound mapper uses, handed the pseudonym: an
                // mmci_patient_<uuid> becomes mmci_clinical_<uuid>.
                ClinicalIdentifier = BiobankMapping.ClinicalIdentifier(patientPseudonym),
                BelongsToPerson = patientPseudonym,

                // ICD-10, and the catalogue's Diagnosis ontology holds Orphanet terms. Nothing here
                // would resolve, so the column goes out empty rather than failing the row - see
                // docs/catalogue-api-contract.md. The codes stay in the domain for the crosswalk
                // that will eventually fill this.
                Diagnosis = null,
                AgeAtDiagnosis = clinical.AgeAtDiagnosis,
                ClinicalTimepoint = clinical.ClinicalTimepoint,
                DiseaseStage = clinical.DiseaseStage,
                MolecularDiagnosisGene = clinical.MolecularDiagnosisGene,
                TreatmentCategory = clinical.TreatmentCategory,
                ResponseToTreatment = clinical.ResponseToTreatment,
            };

    private static MaterialRecord? ToRecord(Material? material, string samplePseudonym, string patientPseudonym) =>
        material is null
            ? null
            : new MaterialRecord
            {
                MaterialIdentifier = samplePseudonym,
                CollectedFromPerson = patientPseudonym,

                // A reference must equal the key it points at or the catalogue's graph breaks
                // silently, so this is derived the same way the clinical identifier is, not copied.
                BelongsToDiagnosis = BiobankMapping.ClinicalIdentifier(patientPseudonym) is { } clinicalId
                    ? [clinicalId]
                    : [],
                SamplingDate = material.SamplingDate,
                MaterialType = material.MaterialType,
                AnatomicalSource = material.AnatomicalSource,
                PathologicalState = material.PathologicalState,
            };

    private static BiospecimenRecord? ToRecord(Biospecimen? biospecimen, string samplePseudonym) =>
        biospecimen is null
            ? null
            : new BiospecimenRecord
            {
                BiospecimenIdentifier = BiobankMapping.BiospecimenIdentifier(samplePseudonym),

                // The material row for the same archived sample, which is keyed by the pseudonym.
                DerivedFromMaterial = samplePseudonym,
                BiospecimenForm = biospecimen.BiospecimenForm,
                PercentageTumorCells = biospecimen.PercentageTumorCells,
                Quantity = biospecimen.Quantity,
                StorageConditions = biospecimen.StorageConditions,
                ManagingBiobank = biospecimen.ManagingBiobank,
                Availability = biospecimen.Availability,
                NameOfFixative = biospecimen.NameOfFixative,
                EmbeddingMedium = biospecimen.EmbeddingMedium,
            };

    private static SamplePreparationRecord ToRecord(SamplePreparation preparation, string samplePseudonym) =>
        new()
        {
            // Already pseudonymized upstream, being derived from the run tree's folder name.
            SampleprepIdentifier = preparation.SampleprepIdentifier,

            // The one identifier here that is not: it points at the biobank's biospecimen.
            BelongsToBiospecimen = BiobankMapping.BiospecimenIdentifier(samplePseudonym),
            InputAmount = preparation.InputAmount,
            LibraryPreparationKit = preparation.LibraryPreparationKit,
            PcrFree = preparation.PcrFree,
            TargetEnrichmentKit = preparation.TargetEnrichmentKit,
            FullySequencedGenes = preparation.FullySequencedGenes,
            PartiallySequencedGenes = preparation.PartiallySequencedGenes,
            UmisPresent = preparation.UmisPresent,
            IntendedInsertSize = preparation.IntendedInsertSize,
            IntendedReadLength = preparation.IntendedReadLength,
            Sequencing = ToRecord(preparation.Sequencing),
        };

    private static SequencingRecord? ToRecord(SequencingRun? run) =>
        run is null
            ? null
            : new SequencingRecord
            {
                SequencingIdentifier = run.SequencingIdentifier,
                BelongsToSamplePreparation = run.BelongsToSamplePreparation,
                SequencingDate = run.SequencingDate,
                SequencingPlatform = run.SequencingPlatform,
                SequencingInstrumentModel = run.SequencingInstrumentModel,
                SequencingMethod = run.SequencingMethod,
                MedianReadDepth = run.MedianReadDepth,
                ObservedReadLength = run.ObservedReadLength,
                ObservedInsertSize = run.ObservedInsertSize,
                PercentageQ30 = run.PercentageQ30,
                PercentageTr20 = run.PercentageTr20,
                OtherQualityMetrics = run.OtherQualityMetrics,
                Analyses = [.. run.Analyses.Select(ToRecord)],
            };

    private static AnalysisRecord ToRecord(Analysis analysis) =>
        new()
        {
            AnalysisIdentifier = analysis.AnalysisIdentifier,
            BelongsToSequencing = analysis.BelongsToSequencing,
            DataFormatsStored = analysis.DataFormatsStored,

            // One text column in the schema, a list in the source.
            AlgorithmsUsed = analysis.AlgorithmsUsed is { Count: > 0 } algorithms
                ? string.Join(", ", algorithms)
                : null,
            ReferenceGenomeUsed = analysis.ReferenceGenomeUsed,
            BioinformaticProtocolUsed = analysis.BioinformaticProtocolUsed,
        };
}
