using BiobankApi.Domain.Patients;
using BiobankApi.Web.Endpoints;

namespace BiobankApi.Web.Mapping;

/// <summary>
/// Projects a <see cref="PatientAggregate"/> onto the <c>GET /patients</c> response contract.
/// The polymorphic sample flatten and the enum-to-string wire vocab are spelled out by hand so the
/// shape and vocabulary stay explicit and controllable.
/// </summary>
internal static class PatientResponseMapper
{
    public static PatientResponse ToResponse(PatientAggregate patient) => new(
        patient.Id.Value,
        patient.Biobank,
        patient.Consent,
        patient.Sex?.ToString().ToLowerInvariant(),
        patient.BirthYear,
        patient.BirthMonth,
        patient.AccessionNumbers,
        patient.Samples.Select(ToResponse).ToList(),
        patient.DiagnosticSpecimens.Select(ToResponse).ToList());

    private static SampleResponse ToResponse(Sample sample)
    {
        // Type-specific fields default to null; each subtype fills its own.
        var (type, diagnosis, pTnm, morphology, cutTime, freezeTime, takingDate, retrieved) = sample switch
        {
            TissueSample t => (MaterialTypes.Tissue, t.Diagnosis, t.PTnm, t.Morphology, t.CutTime, t.FreezeTime,
                (DateTime?)null, t.Retrieved),
            SerumSample s => (MaterialTypes.Serum, s.Diagnosis, null, null, null, null, s.TakingDate, s.Retrieved),
            GenomeSample g => (MaterialTypes.Genome, null, null, null, null, null, g.TakingDate, g.Retrieved),
            _ => throw new InvalidOperationException($"Unknown sample type {sample.GetType().Name}"),
        };

        return new SampleResponse(
            sample.Id.Value,
            type,
            sample.MaterialType,
            sample.MaterialTypeLabel,
            sample.EventNumber,
            sample.CollectionYear,
            sample.Biopsy,
            sample.PredictiveNumber,
            sample.SamplesNo,
            sample.AvailableSamplesNo,
            sample.AccessionNumbers,
            diagnosis,
            pTnm,
            morphology,
            cutTime,
            freezeTime,
            takingDate,
            retrieved?.ToString().ToLowerInvariant());
    }

    private static SpecimenResponse ToResponse(DiagnosticSpecimen specimen) => new(
        specimen.Id.Value,
        specimen.SpecimenNumber,
        specimen.Year,
        specimen.MaterialType,
        specimen.MaterialTypeLabel,
        specimen.Diagnosis,
        specimen.TakingDate,
        specimen.Retrieved?.ToString().ToLowerInvariant());
}
