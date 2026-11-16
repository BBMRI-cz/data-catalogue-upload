namespace TrainingSeeder.Seeding;

internal enum SeedResult
{
    /// <summary>Both databases were empty and now hold the dataset.</summary>
    Seeded,

    /// <summary>A database already held data, so neither was touched.</summary>
    LeftAlone,

    /// <summary>The dataset was written, but some records were refused.</summary>
    Failed,
}

/// <summary>What a seeding run did, counted in each database after it finished.</summary>
/// <param name="Failures">Each refused record as <c>source reference: reason</c>.</param>
internal sealed record SeedOutcome(
    SeedResult Result,
    int Patients,
    int Samples,
    int Runs,
    int Panels,
    IReadOnlyList<string> Failures);
