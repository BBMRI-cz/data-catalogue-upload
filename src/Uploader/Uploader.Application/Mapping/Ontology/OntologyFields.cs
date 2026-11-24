using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// Every ontology column the uploader fills. A run reads the live term list of each of them before the
/// first patient, and stops before syncing anything if one is not an ontology column in the live
/// schema - resolving against a list that is not there would leave every value of it out, and a save
/// replaces the whole row.
/// <para>
/// Table ids and column names are spelled as EMX2 spells them in a GraphQL mutation, which is also how
/// the payload records serialize.
/// </para>
/// </summary>
public static class OntologyFields
{
    public static readonly OntologyField GenderAtBirth = new("Personal", "genderAtBirth");
    public static readonly OntologyField PrimaryAffiliatedInstitute = new("Personal", "primaryAffiliatedInstitute");
    public static readonly OntologyField Diagnosis = new("Clinical", "diagnosis");

    public static readonly OntologyField MaterialType = new("Material", "materialType");
    public static readonly OntologyField PathologicalState = new("Material", "pathologicalState");

    public static readonly OntologyField BiospecimenForm = new("Biospecimens", "biospecimenForm");
    public static readonly OntologyField StorageConditions = new("Biospecimens", "storageConditions");
    public static readonly OntologyField Availability = new("Biospecimens", "availability");
    public static readonly OntologyField ManagingBiobank = new("Biospecimens", "managingBiobank");

    public static readonly OntologyField LibraryPreparationKit = new("SamplePreparation", "libraryPreparationKit");
    public static readonly OntologyField TargetEnrichmentKit = new("SamplePreparation", "targetEnrichmentKit");
    public static readonly OntologyField FullySequencedGenes = new("SamplePreparation", "fullySequencedGenes");
    public static readonly OntologyField PartiallySequencedGenes = new("SamplePreparation", "partiallySequencedGenes");

    public static readonly OntologyField SequencingPlatform = new("Sequencing", "sequencingPlatform");
    public static readonly OntologyField SequencingInstrumentModel = new("Sequencing", "sequencingInstrumentModel");
    public static readonly OntologyField SequencingMethod = new("Sequencing", "sequencingMethod");

    public static readonly OntologyField DataFormatsStored = new("Analysis", "dataFormatsStored");
    public static readonly OntologyField ReferenceGenomeUsed = new("Analysis", "referenceGenomeUsed");

    public static readonly IReadOnlyList<OntologyField> All =
    [
        GenderAtBirth,
        PrimaryAffiliatedInstitute,
        Diagnosis,
        MaterialType,
        PathologicalState,
        BiospecimenForm,
        StorageConditions,
        Availability,
        ManagingBiobank,
        LibraryPreparationKit,
        TargetEnrichmentKit,
        FullySequencedGenes,
        PartiallySequencedGenes,
        SequencingPlatform,
        SequencingInstrumentModel,
        SequencingMethod,
        DataFormatsStored,
        ReferenceGenomeUsed,
    ];
}
