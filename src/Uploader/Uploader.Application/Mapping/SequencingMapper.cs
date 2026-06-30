using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>Maps the raw sequencing DTO onto the <see cref="SequencingAggregate"/> (entry + prep chain).</summary>
public static class SequencingMapper
{
    public static ErrorOr<SequencingAggregate> ToSequencing(SequencingDto dto, SequencingId id, SampleId sampleId) =>
        SequencingAggregate.Create(id.Value, sampleId, ToEntries(dto));

    private static List<SequencingEntry> ToEntries(SequencingDto dto) =>
        dto.SequencingEntries is { Count: > 0 }
            ? [.. dto.SequencingEntries.Select(ToEntry)]
            : [ToEntry(new SequencingEntryDto { FixedBlock = dto.FixedBlock, SamplePreparation = dto.SamplePreparation })];

    private static SequencingEntry ToEntry(SequencingEntryDto dto) => new()
    {
        FixedBlockId = OptionalFixedBlockId(dto.FixedBlock?.BlockIdentifier),
        SamplePreparation = ToSamplePreparation(dto.SamplePreparation),
    };

    private static SamplePreparation? ToSamplePreparation(SamplePreparationDto? dto) =>
        dto is null
            ? null
            : new SamplePreparation
            {
                SampleprepIdentifier = dto.SampleprepIdentifier,
                BelongsToMaterial = dto.BelongsToMaterial,
                InputAmount = dto.InputAmount,
                LibraryPreparationKit = dto.LibraryPreparationKit,
                PcrFree = dto.PcrFree,
                TargetEnrichmentKit = dto.TargetEnrichmentKit,
                FullSequenceGenes = dto.FullSequenceGenes,
                PartialSequenceGenes = dto.PartialSequenceGenes,
                UmisPresent = dto.UmisPresent,
                IntendedInsertSize = dto.IntendedInsertSize,
                IntendedReadLength = dto.IntendedReadLength,
                Sequencing = ToSequencingRun(dto.Sequencing),
            };

    // The source returns analyses as an array; the domain keeps the first.
    private static SequencingRun? ToSequencingRun(SequencingRunDto? dto) =>
        dto is null
            ? null
            : new SequencingRun
            {
                SequencingIdentifier = dto.SequencingIdentifier,
                BelongsToSamplePreparation = dto.BelongsToSamplePreparation,
                SequencingDate = dto.SequencingDate,
                SequencingPlatform = dto.SequencingPlatform,
                SequencingInstrumentModel = dto.SequencingInstrumentModel,
                SequencingMethod = dto.SequencingMethod,
                MedianReadDepth = dto.MedianReadDepth,
                ObservedReadLength = dto.ObservedReadLength,
                ObservedInsertSize = dto.ObservedInsertSize,
                PercentageQ30 = dto.PercentageQ30,
                PercentageTr20 = dto.PercentageTr20,
                OtherQualityMetrics = dto.OtherQualityMetrics,
                Analysis = dto.Analyses is { Count: > 0 } ? ToAnalysis(dto.Analyses[0]) : null,
            };

    private static Analysis ToAnalysis(AnalysisDto dto) => new()
    {
        AnalysisIdentifier = dto.AnalysisIdentifier,
        BelongsToSequencing = dto.BelongsToSequencing,
        PhysicalDataLocation = dto.PhysicalDataLocation,
        AbstractDataLocation = dto.AbstractDataLocation,
        DataFormatsStored = dto.DataFormatsStored,
        AlgorithmsUsed = dto.AlgorithmsUsed,
        ReferenceGenomeUsed = dto.ReferenceGenomeUsed,
        BioinformaticProtocolUsed = dto.BioinformaticProtocolUsed,
        BioinformaticProtocolDeviation = dto.BioinformaticProtocolDeviation,
        ReasonForBioinformaticProtocolDeviation = dto.ReasonForBioinformaticProtocolDeviation,
        WgsGuidelineFollowed = dto.WgsGuidelineFollowed,
    };

    private static FixedBlockId? OptionalFixedBlockId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new FixedBlockId(value);
}
