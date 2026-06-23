using ErrorOr;
using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>
/// Imaging study aggregate root, keyed by radiology accession number
/// (<see cref="AccessionNumber"/>) and linked to its <see cref="PatientAggregate"/> by id. Owns the
/// modality series (CT, MR, US, DX, MG) as value objects.
/// </summary>
public sealed class ImagingStudyAggregate : AggregateRoot<AccessionNumber>
{
    private ImagingStudyAggregate()
    {
    }

    public PatientId PatientId { get; private init; }
    public string? ImagingStudyIdentifier { get; private init; }
    public string? BelongsToPerson { get; private init; }
    public IReadOnlyList<string>? ImagingModality { get; private init; }
    public IReadOnlyList<string>? BodyRegion { get; private init; }
    public IReadOnlyList<string>? ImagingProcedure { get; private init; }
    public IReadOnlyList<string>? ReasonForImagingProcedure { get; private init; }
    public string? StudyStartDate { get; private init; }
    public int? DicomSeriesCount { get; private init; }
    public int? DicomImagesCount { get; private init; }
    public string? AffiliatedInstitution { get; private init; }
    public CtSeries? CtSeries { get; private init; }
    public MrSeries? MrSeries { get; private init; }
    public UsSeries? UsSeries { get; private init; }
    public DxSeries? DxSeries { get; private init; }
    public MgSeries? MgSeries { get; private init; }

    /// <summary>
    /// Fingerprint over the whole study (serialized by property name, so the routing ids — stable
    /// for a given accession number — never cause spurious updates).
    /// </summary>
    public Fingerprint ComputeFingerprint() => Fingerprint.Of(this);

    public static ErrorOr<ImagingStudyAggregate> Create(string? accessionNumber, PatientId patientId, Details details)
    {
        if (string.IsNullOrWhiteSpace(accessionNumber))
        {
            return Error.Validation("ImagingStudy.AccessionNumber", "Imaging study accession number is required.");
        }

        return new ImagingStudyAggregate
        {
            Id = new AccessionNumber(accessionNumber),
            PatientId = patientId,
            ImagingStudyIdentifier = details.ImagingStudyIdentifier,
            BelongsToPerson = details.BelongsToPerson,
            ImagingModality = details.ImagingModality,
            BodyRegion = details.BodyRegion,
            ImagingProcedure = details.ImagingProcedure,
            ReasonForImagingProcedure = details.ReasonForImagingProcedure,
            StudyStartDate = details.StudyStartDate,
            DicomSeriesCount = details.DicomSeriesCount,
            DicomImagesCount = details.DicomImagesCount,
            AffiliatedInstitution = details.AffiliatedInstitution,
            CtSeries = details.CtSeries,
            MrSeries = details.MrSeries,
            UsSeries = details.UsSeries,
            DxSeries = details.DxSeries,
            MgSeries = details.MgSeries,
        };
    }

    /// <summary>Carrier for the imaging study's descriptive content, keeping <see cref="Create"/> tidy.</summary>
    public sealed record Details
    {
        public string? ImagingStudyIdentifier { get; init; }
        public string? BelongsToPerson { get; init; }
        public IReadOnlyList<string>? ImagingModality { get; init; }
        public IReadOnlyList<string>? BodyRegion { get; init; }
        public IReadOnlyList<string>? ImagingProcedure { get; init; }
        public IReadOnlyList<string>? ReasonForImagingProcedure { get; init; }
        public string? StudyStartDate { get; init; }
        public int? DicomSeriesCount { get; init; }
        public int? DicomImagesCount { get; init; }
        public string? AffiliatedInstitution { get; init; }
        public CtSeries? CtSeries { get; init; }
        public MrSeries? MrSeries { get; init; }
        public UsSeries? UsSeries { get; init; }
        public DxSeries? DxSeries { get; init; }
        public MgSeries? MgSeries { get; init; }
    }
}
