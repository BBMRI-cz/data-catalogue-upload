using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the domain onto the FAIR Genomes rows the catalogue receives, substituting a pseudonym for
/// every real identifier on the way out. This is the only place the two meet: aggregates, sync state
/// and fingerprints all stay keyed on the real ids, which is what lets a later run recognise the
/// same patient, and nothing real is written past this boundary.
/// <para>
/// Only two pseudonyms are needed. The sequencing chain arrives pseudonymized already - its
/// identifiers derive from the sequencing API's sample id, which is the run tree's
/// <c>mmci_predictive_&lt;uuid&gt;</c> folder name - and the derived clinical identifier falls out of
/// <see cref="BiobankMapping.ClinicalIdentifier"/> once that helper is handed the patient's
/// pseudonym instead of the patient's real id. That is what it was written for.
/// </para>
/// </summary>
public static class CatalogueMapper
{
    public static CataloguePatientPayload ToPayload(PatientAggregate patient, string patientPseudonym) =>
        new()
        {
            ExternalId = patientPseudonym,
            Personal = ToRecord(patient.Personal, patientPseudonym),
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
        };

    public static CatalogueSequencingPayload ToPayload(SequencingAggregate sequencing, string samplePseudonym) =>
        new()
        {
            ExternalId = samplePseudonym,
            SampleId = samplePseudonym,
            SamplePreparations =
                [.. sequencing.Preparations.Select(preparation => ToRecord(preparation, samplePseudonym))],
        };

    private static PersonalRecord? ToRecord(Personal? personal, string patientPseudonym) =>
        personal is null
            ? null
            : new PersonalRecord
            {
                PersonalIdentifier = patientPseudonym,
                YearOfBirth = personal.YearOfBirth,
                GenderAtBirth = personal.GenderAtBirth,
                GenderIdentity = personal.GenderIdentity,
            };

    private static ClinicalRecord? ToRecord(Clinical? clinical, string patientPseudonym) =>
        clinical is null
            ? null
            : new ClinicalRecord
            {
                // The same helper the inbound mapper uses, handed the pseudonym: an
                // mmci_patient_<uuid> becomes mmci_clinical_<uuid>.
                ClinicalIdentifier = BiobankMapping.ClinicalIdentifier(patientPseudonym),
                BelongsToPerson = patientPseudonym,
                ClinicalDiagnosis = clinical.ClinicalDiagnosis,
                AgeAtDiagnosis = clinical.AgeAtDiagnosis,
                AgeOfOnset = clinical.AgeOfOnset,
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
                SamplingTimestamp = material.SamplingTimestamp,
                RegistrationTimestamp = material.RegistrationTimestamp,
                SamplingProtocol = material.SamplingProtocol,
                SamplingProtocolDeviation = material.SamplingProtocolDeviation,
                ReasonForSamplingProtocolDeviation = material.ReasonForSamplingProtocolDeviation,
                BiospecimenType = material.BiospecimenType,
                AnatomicalSource = material.AnatomicalSource,
                PathologicalState = material.PathologicalState,
                StorageConditions = material.StorageConditions,
                ExpirationDate = material.ExpirationDate,
                PercentageTumorCells = material.PercentageTumorCells,
                PhysicalLocation = material.PhysicalLocation,
                AnalysesPerformed = material.AnalysesPerformed,

                // Deliberately not carried. It references the material this one was derived from,
                // which is a different sample's real id: pseudonymizing it needs that sample's
                // pseudonym, not this one's. No source sets it today, and whoever wires it up should
                // have to resolve it here on purpose rather than have a real id forwarded silently.
                DerivedFrom = null,
            };

    private static SamplePreparationRecord ToRecord(SamplePreparation preparation, string samplePseudonym) =>
        new()
        {
            // Already pseudonymized upstream, being derived from the run tree's folder name.
            SampleprepIdentifier = preparation.SampleprepIdentifier,

            // The one identifier here that is not: it points at the biobank's material.
            BelongsToMaterial = samplePseudonym,
            InputAmount = preparation.InputAmount,
            LibraryPreparationKit = preparation.LibraryPreparationKit,
            PcrFree = preparation.PcrFree,
            TargetEnrichmentKit = preparation.TargetEnrichmentKit,
            FullSequenceGenes = preparation.FullSequenceGenes,
            PartialSequenceGenes = preparation.PartialSequenceGenes,
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
            PhysicalDataLocation = analysis.PhysicalDataLocation,
            AbstractDataLocation = analysis.AbstractDataLocation,
            DataFormatsStored = analysis.DataFormatsStored,
            AlgorithmsUsed = analysis.AlgorithmsUsed,
            ReferenceGenomeUsed = analysis.ReferenceGenomeUsed,
            BioinformaticProtocolUsed = analysis.BioinformaticProtocolUsed,
            BioinformaticProtocolDeviation = analysis.BioinformaticProtocolDeviation,
            ReasonForBioinformaticProtocolDeviation = analysis.ReasonForBioinformaticProtocolDeviation,
            WgsGuidelineFollowed = analysis.WgsGuidelineFollowed,
        };
}
