using System.Globalization;
using ErrorOr;
using Microsoft.Extensions.Configuration;

namespace TrainingSeeder;

/// <summary>What the seeder generates, from <c>SEED_PATIENTS</c> and <c>SEED</c>.</summary>
/// <param name="Patients">How many patients; null means as many as the real database has.</param>
/// <param name="Seed">The random seed. The same seed and size always give the same dataset.</param>
internal sealed record SeedSettings(int? Patients, int Seed)
{
    /// <summary>The seed the training stack uses unless told otherwise: the issue that asked for it.</summary>
    public const int DefaultSeed = 112;

    public static ErrorOr<SeedSettings> FromConfiguration(IConfiguration configuration)
    {
        var patients = Positive(configuration["SEED_PATIENTS"], "SEED_PATIENTS");
        if (patients.IsError)
        {
            return patients.Errors;
        }

        var seed = configuration["SEED"];
        if (string.IsNullOrWhiteSpace(seed))
        {
            return new SeedSettings(patients.Value, DefaultSeed);
        }

        return int.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? new SeedSettings(patients.Value, value)
            : Error.Validation("SEED", $"SEED must be a whole number, got '{seed}'.");
    }

    // Blank means unset, so compose can pass ${SEED_PATIENTS:-} through.
    private static ErrorOr<int?> Positive(string? raw, string name)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (int?)null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : Error.Validation(name, $"{name} must be a positive whole number, got '{raw}'.");
    }
}
