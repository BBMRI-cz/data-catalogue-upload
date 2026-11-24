using System.Globalization;
using ErrorOr;
using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the biobank's sample record straight onto the catalogue's sample rows, which FAIR Genomes v2
/// splits in two: the <c>Material</c> collected from the patient and the <c>Biospecimens</c> row stored
/// from it. It also builds the <see cref="SampleAggregate"/> the sync planner keeps them under, with the
/// real ids and the links to the sample's sequencing and WSI. Placing a value and swapping in the
/// pseudonyms happen in one step, so the payload never holds a real identifier.
/// <para>
/// The material code goes to the run's <see cref="TermResolver"/> exactly as the biobank served it
/// (<c>53</c>, <c>SD</c>), once for each of its four columns, with the sample type and the biobank
/// API's label as its <see cref="TermContext"/>; the patient's biobank code goes to it for the
/// managing biobank. Availability is worked out here from the units left and only checked against
/// the live list. A value the resolver cannot place is left out of a row that still goes out. The
/// biospecimen identifier and the diagnosis reference are derived from the pseudonyms by
/// <see cref="BiobankMapping"/> rather than copied, so a reference cannot drift from the key it
/// points at.
/// </para>
/// <para>
/// Deliberately dropped, because no catalogue column takes them yet: <c>p_tnm</c>, <c>morphology</c>,
/// <c>retrieved</c>, <c>event_number</c>, <c>collection_year</c>, <c>samples_no</c> (superseded by the
/// available count), <c>diagnosis</c> (it goes to the patient's <c>Clinical</c> row) and
/// <c>accession_numbers</c> (they key the radiology lookup). <c>type</c> and
/// <c>material_type_label</c> are the material code's context and which date applies rather than
/// being stored, and <c>predictive_number</c> and <c>biopsy</c> are links for the planner, never
/// published.
/// </para>
/// </summary>
public static class CatalogueSampleMapper
{
    /// <summary>What the sync planner needs of the sample: its real ids and the links it owns.</summary>
    public static ErrorOr<SampleAggregate> ToSample(SampleDto dto, PatientId patientId) =>
        SampleAggregate.Create(
            dto.SampleId,
            patientId,
            OptionalSequencingId(dto.PredictiveNumber),
            // ponytail: no WSI link from this source yet. The key is `biopsy` ("2023/2872-1"): the
            // previous uploader found the scans under <year>/<first 2>/<rest> as "2023_02872-01"
            // (case zero-padded to 5, block to 2). Fill this in with #31, once the WSI service names
            // its key.
            wsiId: null);

    /// <param name="biobank">The patient's biobank code, which names the managing biobank.</param>
    /// <param name="misses">Where the values the resolver could not place are recorded.</param>
    public static CatalogueSamplePayload ToPayload(
        SampleDto dto,
        string samplePseudonym,
        string patientPseudonym,
        string? biobank,
        TermResolver terms,
        ICollection<UnmappedValue> misses)
    {
        // One material code fills four columns, and what it means depends on the sample type.
        var material = TermContext.Material(dto.Type, dto.MaterialTypeLabel);
        string? MaterialTerm(OntologyField field) => terms.Resolve(field, dto.MaterialType, material, misses);

        return new()
        {
            ExternalId = samplePseudonym,
            PatientId = patientPseudonym,
            Material = new MaterialRecord
            {
                MaterialIdentifier = samplePseudonym,
                CollectedFromPerson = patientPseudonym,
                BelongsToDiagnosis = BelongsToDiagnosis(patientPseudonym),

                // v2 asks for the collection date rather than v1's pair of timestamps. Tissue is dated
                // by when it was cut; everything else by when it was taken.
                SamplingDate = Date(BiobankMapping.IsTissue(dto.Type) ? dto.CutTime : dto.TakingDate),
                MaterialType = MaterialTerm(OntologyFields.MaterialType),
                PathologicalState = MaterialTerm(OntologyFields.PathologicalState),
            },
            Biospecimen = new BiospecimenRecord
            {
                BiospecimenIdentifier = BiobankMapping.BiospecimenIdentifier(samplePseudonym),

                // The material row for the same archived sample, which is keyed by the pseudonym.
                DerivedFromMaterial = samplePseudonym,
                BiospecimenForm = MaterialTerm(OntologyFields.BiospecimenForm),
                StorageConditions = MaterialTerm(OntologyFields.StorageConditions),

                // What a researcher could still request, not what was once collected.
                Quantity = dto.AvailableSamplesNo,
                Availability = terms.Resolve(
                    OntologyFields.Availability, AvailabilityOf(dto.AvailableSamplesNo), misses),
                ManagingBiobank = terms.Resolve(OntologyFields.ManagingBiobank, biobank, misses),
            },
        };
    }

    /// <summary>
    /// The patient's single clinical record - the diagnosis this material belongs to. Derived the same
    /// way <see cref="CataloguePatientMapper"/> derives the clinical key, so the two always match.
    /// </summary>
    private static IReadOnlyList<string> BelongsToDiagnosis(string patientPseudonym) =>
        BiobankMapping.ClinicalIdentifier(patientPseudonym) is { } clinicalId ? [clinicalId] : [];

    /// <summary>
    /// <c>Biospecimens.Availability</c>, worked out from the count of units still on the shelf rather
    /// than translated, so it is ours to compute and not the mapping table's. The export distinguishes
    /// only "some left" from "none left", so Reserved and Destroyed never apply. The term still goes
    /// through the resolver, so a catalogue that drops one shows it as unmapped.
    /// </summary>
    private static string? AvailabilityOf(int? availableSamples) => availableSamples switch
    {
        null => null,
        0 => "Depleted",
        _ => "Available",
    };

    private static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);
}
