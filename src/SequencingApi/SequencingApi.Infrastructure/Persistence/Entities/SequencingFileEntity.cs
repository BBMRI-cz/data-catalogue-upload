using SequencingApi.Domain;

namespace SequencingApi.Infrastructure.Persistence.Entities;

/// <summary>
/// EF Core persistence row for the <c>sequencing_file</c> table — one table for every artifact,
/// discriminated by <see cref="Role"/>, mirroring the domain's single file type.
/// </summary>
public class SequencingFileEntity
{
    public long Id { get; set; }

    /// <summary>
    /// Set when the file came straight off the sequencer. Exactly one of this and
    /// <see cref="AnalysisId"/> is non-null; a check constraint enforces it.
    /// </summary>
    public long? RunSampleId { get; set; }

    /// <summary>Set when an analysis produced the file (alignments, variant calls, reports).</summary>
    public long? AnalysisId { get; set; }

    public FileRole Role { get; set; }
    public string Path { get; set; } = default!;
    public string? Format { get; set; }
    public int? Lane { get; set; }
    public int? Read { get; set; }
    public long? SizeBytes { get; set; }
    public string? Checksum { get; set; }
}
