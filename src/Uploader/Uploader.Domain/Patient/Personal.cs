using Uploader.Domain.Common;

namespace Uploader.Domain;

/// <summary>FAIR Genomes <c>Personal</c> value object.</summary>
public sealed record Personal : ValueObject
{
    public string? PersonalIdentifier { get; init; }
    public int? YearOfBirth { get; init; }
    public string? GenderAtBirth { get; init; }
    public string? GenderIdentity { get; init; }
}
