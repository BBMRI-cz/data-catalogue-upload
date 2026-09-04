namespace Uploader.Application.Dtos;

/// <summary>A sequencing read or an analysis output, told apart by which list it sits in.</summary>
public sealed record SequencingFileDto
{
    public string? Role { get; init; }
    public string? Path { get; init; }
    public string? Format { get; init; }
    public int? Lane { get; init; }
    public int? Read { get; init; }
    public long? SizeBytes { get; init; }
    public string? Checksum { get; init; }
}
