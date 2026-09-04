namespace Uploader.Application.Dtos;

/// <summary>
/// The sequencing API's <c>GET /sequencing?predictive_number=</c> payload. Property names mirror that
/// service's own response records so <c>SnakeCaseLower</c> on both sides lines the wire keys up;
/// <c>SequencingContractParityTests</c> guards that. Enum-valued fields arrive as strings — the source
/// converts them before serializing — and every optional field is written as an explicit <c>null</c>
/// rather than omitted, so an absent key means a contract change, not an absent value.
/// </summary>
public sealed record SequencingDto
{
    public string? PredictiveNumber { get; init; }
    public IReadOnlyList<SequencingSampleDto>? Samples { get; init; }
}
