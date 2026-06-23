using ErrorOr;
using Riok.Mapperly.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Domain;
using Uploader.Domain.Common;

namespace Uploader.Application.Mapping;

/// <summary>
/// Maps the raw source DTOs onto the domain aggregates. The aggregate roots are produced through
/// their <c>Create</c> factories (returning <see cref="ErrorOr{T}"/> on invalid source data);
/// Mapperly generates the tedious value-object field copies (imaging series, slide chain,
/// preparation chain).
/// </summary>
[Mapper]
public sealed partial class SourceMapper
{
    // ---------------------------------------------------------------- Patient
    public ErrorOr<PatientAggregate> ToPatient(PatientDto dto) =>
        PatientAggregate.Create(dto.PatientId, ToPersonal(dto), ToClinical(dto));

    private partial Personal ToPersonal(PatientDto dto);

    private partial Clinical ToClinical(PatientDto dto);

    // ---------------------------------------------------------------- Sample
    public ErrorOr<SampleAggregate> ToSample(SampleDto dto, PatientId patientId) =>
        SampleAggregate.Create(
            dto.SampleId,
            patientId,
            OptionalSequencingId(dto.PredictiveNumber),
            OptionalWsiId(dto.BiopticNumber),
            ToMaterial(dto));

    private partial Material ToMaterial(SampleDto dto);

    // ---------------------------------------------------------------- Sequencing
    public ErrorOr<SequencingAggregate> ToSequencing(SequencingDto dto, SequencingId id, SampleId sampleId) =>
        SequencingAggregate.Create(id.Value, sampleId, ToEntries(dto));

    private List<SequencingEntry> ToEntries(SequencingDto dto) =>
        dto.SequencingEntries is { Count: > 0 }
            ? [.. dto.SequencingEntries.Select(ToEntry)]
            : [ToEntry(new SequencingEntryDto { FixedBlock = dto.FixedBlock, SamplePreparation = dto.SamplePreparation })];

    private SequencingEntry ToEntry(SequencingEntryDto dto) => new()
    {
        FixedBlockId = OptionalFixedBlockId(dto.FixedBlock?.BlockIdentifier),
        SamplePreparation = ToSamplePreparation(dto.SamplePreparation),
    };

    private partial SamplePreparation? ToSamplePreparation(SamplePreparationDto? dto);

    // The source returns analyses as an array; the domain keeps the first.
    private SequencingRun? ToSequencingRun(SequencingRunDto? dto) =>
        dto is null
            ? null
            : MapSequencingRun(dto) with
            {
                Analysis = dto.Analyses is { Count: > 0 } ? ToAnalysis(dto.Analyses[0]) : null,
            };

    [MapperIgnoreTarget(nameof(SequencingRun.Analysis))]
    private partial SequencingRun MapSequencingRun(SequencingRunDto dto);

    private partial Analysis ToAnalysis(AnalysisDto dto);

    // ---------------------------------------------------------------- WSI
    public ErrorOr<WsiAggregate> ToWsi(WsiDto dto, WsiId id, SampleId sampleId) =>
        WsiAggregate.Create(id.Value, sampleId, ToFixedBlock(dto));

    private FixedBlock? ToFixedBlock(WsiDto dto)
    {
        if (dto.BlockIdentifier is null
            && dto.SourceMaterial is null
            && dto.NameOfFixative is null
            && dto.EmbeddingMedium is null
            && dto.SlideContainer is null)
        {
            return null;
        }

        return new FixedBlock
        {
            Id = OptionalFixedBlockId(dto.BlockIdentifier),
            SourceMaterial = dto.SourceMaterial,
            NameOfFixative = dto.NameOfFixative,
            EmbeddingMedium = dto.EmbeddingMedium,
            SlideContainer = ToSlideContainer(dto.SlideContainer),
        };
    }

    private partial SlideContainer? ToSlideContainer(SlideContainerDto? dto);

    // ---------------------------------------------------------------- Imaging study
    public ErrorOr<ImagingStudyAggregate> ToImagingStudy(ImagingStudyDto dto, PatientId patientId) =>
        ImagingStudyAggregate.Create(dto.AccessionNumber, patientId, new ImagingStudyAggregate.Details
        {
            ImagingStudyIdentifier = dto.ImagingStudyIdentifier,
            BelongsToPerson = dto.BelongsToPerson,
            ImagingModality = dto.ImagingModality,
            BodyRegion = dto.BodyRegion,
            ImagingProcedure = dto.ImagingProcedure,
            ReasonForImagingProcedure = dto.ReasonForImagingProcedure,
            StudyStartDate = dto.StudyStartDate,
            DicomSeriesCount = dto.DicomSeriesCount,
            DicomImagesCount = dto.DicomImagesCount,
            AffiliatedInstitution = dto.AffiliatedInstitution,
            CtSeries = ToCtSeries(dto.CtSeries),
            MrSeries = ToMrSeries(dto.MrSeries),
            UsSeries = ToUsSeries(dto.UsSeries),
            DxSeries = ToDxSeries(dto.DxSeries),
            MgSeries = ToMgSeries(dto.MgSeries),
        });

    private partial CtSeries? ToCtSeries(CtSeriesDto? dto);

    private partial MrSeries? ToMrSeries(MrSeriesDto? dto);

    private partial UsSeries? ToUsSeries(UsSeriesDto? dto);

    private partial DxSeries? ToDxSeries(DxSeriesDto? dto);

    private partial MgSeries? ToMgSeries(MgSeriesDto? dto);

    // ---------------------------------------------------------------- Identifier helpers
    private static SequencingId? OptionalSequencingId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new SequencingId(value);

    private static WsiId? OptionalWsiId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new WsiId(value);

    private static FixedBlockId? OptionalFixedBlockId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new FixedBlockId(value);
}
