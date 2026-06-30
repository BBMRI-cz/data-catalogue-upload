using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>Maps the raw biobank sample DTO onto the <see cref="SampleAggregate"/>.</summary>
public static class SampleMapper
{
    public static ErrorOr<SampleAggregate> ToSample(SampleDto dto, PatientId patientId) =>
        SampleAggregate.Create(
            dto.SampleId,
            patientId,
            OptionalSequencingId(dto.PredictiveNumber),
            OptionalWsiId(dto.BiopticNumber),
            ToMaterial(dto));

    private static Material ToMaterial(SampleDto dto) => new()
    {
        MaterialIdentifier = dto.MaterialIdentifier,
        CollectedFromPerson = dto.CollectedFromPerson,
        BelongsToDiagnosis = dto.BelongsToDiagnosis,
        SamplingTimestamp = dto.SamplingTimestamp,
        RegistrationTimestamp = dto.RegistrationTimestamp,
        SamplingProtocol = dto.SamplingProtocol,
        SamplingProtocolDeviation = dto.SamplingProtocolDeviation,
        ReasonForSamplingProtocolDeviation = dto.ReasonForSamplingProtocolDeviation,
        BiospecimenType = dto.BiospecimenType,
        AnatomicalSource = dto.AnatomicalSource,
        PathologicalState = dto.PathologicalState,
        StorageConditions = dto.StorageConditions,
        ExpirationDate = dto.ExpirationDate,
        PercentageTumorCells = dto.PercentageTumorCells,
        PhysicalLocation = dto.PhysicalLocation,
        AnalysesPerformed = dto.AnalysesPerformed,
        DerivedFrom = dto.DerivedFrom,
    };

    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);

    private static WsiId? OptionalWsiId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new WsiId(value);
}
