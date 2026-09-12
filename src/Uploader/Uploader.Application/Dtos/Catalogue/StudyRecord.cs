namespace Uploader.Application.Dtos;

/// <summary>
/// EMX2 <c>Study</c> row. One per deployment, configured rather than derived from any source, and
/// upserted once at the start of a run so every <c>Personal</c> has something to reference. It
/// holds no patient data, so nothing in it is pseudonymized.
/// </summary>
public sealed record StudyRecord
{
    public required string Identifier { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? PrincipalInvestigator { get; init; }
    public string? ContactInformation { get; init; }
    public string? StudyDesign { get; init; }
    public string? StartDate { get; init; }
    public string? CompletionDate { get; init; }
}
