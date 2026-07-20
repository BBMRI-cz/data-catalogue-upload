using ErrorOr;
using SequencingApi.Domain.Common;

namespace SequencingApi.Domain.Samples;

/// <summary>
/// One file produced by sequencing or by an analysis of it, identified by what it is
/// (<see cref="FileRole"/>) rather than by which column of a table it came from.
/// </summary>
/// <remarks>
/// One generic file type instead of a property per format: reads, alignments, variant calls and
/// reports all differ in role, not in structure, so a source that emits a new kind of artifact
/// needs no new domain type.
/// </remarks>
public sealed record SequencingFile : ValueObject
{
    // Internal (not private) so the persistence mapper can reconstitute loaded rows; the public
    // creation path stays Create. A record's implicit parameterless constructor would otherwise be
    // public and let callers bypass Create entirely.
    internal SequencingFile()
    {
    }

    public required FileRole Role { get; init; }

    /// <summary>Where the file lives, as the source addresses it.</summary>
    public required string Path { get; init; }

    /// <summary>File format, when the source distinguishes it from the role.</summary>
    public string? Format { get; init; }

    /// <summary>Flowcell lane this file's reads came from. Reads only.</summary>
    public int? Lane { get; init; }

    /// <summary>Which read of a fragment this file holds — 1 or 2 for paired-end. Reads only.</summary>
    public int? Read { get; init; }

    /// <summary>Size in bytes. Zero is a legitimate value: empty files occur in the sources.</summary>
    public long? SizeBytes { get; init; }

    public string? Checksum { get; init; }

    public static ErrorOr<SequencingFile> Create(
        FileRole role,
        string path,
        string? format = null,
        int? lane = null,
        int? read = null,
        long? sizeBytes = null,
        string? checksum = null)
    {
        if (Normalize.Text(path) is not { } cleanPath)
        {
            return Error.Validation("SequencingFile.Path", "path must not be empty");
        }

        if (lane is < 1)
        {
            return Error.Validation("SequencingFile.Lane", $"lane must be positive, got {lane}");
        }

        if (read is < 1)
        {
            return Error.Validation("SequencingFile.Read", $"read must be positive, got {read}");
        }

        // Note the boundary: negative is impossible, but zero is not rejected — a zero-byte file is
        // a real state the sources produce, and the model has to be able to say so.
        if (sizeBytes is < 0)
        {
            return Error.Validation("SequencingFile.SizeBytes", $"size_bytes must not be negative, got {sizeBytes}");
        }

        return new SequencingFile
        {
            Role = role,
            Path = cleanPath,
            Format = Normalize.Lower(format),
            Lane = lane,
            Read = read,
            SizeBytes = sizeBytes,
            Checksum = Normalize.Lower(checksum),
        };
    }
}
