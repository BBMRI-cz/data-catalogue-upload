using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace SequencingApi.Infrastructure.Configuration;

/// <summary>
/// Runtime configuration for the sequencing API, read from environment variables (the same shape the
/// biobank service uses). Defaults keep the app runnable without a configured environment.
/// </summary>
public sealed class SequencingOptions
{
    public string PostgresUser { get; init; } = "postgres";
    public string PostgresPassword { get; init; } = "postgres";
    public string PostgresDb { get; init; } = "sequencing_api";
    public string PostgresHost { get; init; } = "localhost";
    public int PostgresPort { get; init; } = 5434;
    public string SequencingHost { get; init; } = "0.0.0.0";
    public int SequencingPort { get; init; } = 8002;

    /// <summary>Root of the organised sequencing run tree — the primary data source.</summary>
    public string SequencingDataPath { get; init; } = "data/organised-runs";

    /// <summary>
    /// Directory holding the versioned libraries table and its BED files. Separate from the run tree
    /// because it is maintained by hand, outside the sequencing pipeline that produces the runs.
    /// </summary>
    public string SequencingLibrariesPath { get; init; } = "data/libraries";

    /// <summary>
    /// Directory holding the pseudonymizer's mapping files. Only the predictive-number mapping is
    /// read; the patient and sample mappings beside it are out of this service's scope.
    /// </summary>
    public string SequencingMappingTablePath { get; init; } = "data/mapping-table";

    public string ConnectionString =>
        $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    public static SequencingOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new SequencingOptions();
        return new SequencingOptions
        {
            PostgresUser = configuration["POSTGRES_USER"] ?? defaults.PostgresUser,
            PostgresPassword = configuration["POSTGRES_PASSWORD"] ?? defaults.PostgresPassword,
            PostgresDb = configuration["POSTGRES_DB"] ?? defaults.PostgresDb,
            PostgresHost = configuration["POSTGRES_HOST"] ?? defaults.PostgresHost,
            PostgresPort = ParseInt(configuration["POSTGRES_PORT"], defaults.PostgresPort),
            SequencingHost = configuration["SEQUENCING_HOST"] ?? defaults.SequencingHost,
            SequencingPort = ParseInt(configuration["SEQUENCING_PORT"], defaults.SequencingPort),
            SequencingDataPath = configuration["SEQUENCING_DATA_PATH"] ?? defaults.SequencingDataPath,
            SequencingLibrariesPath = configuration["SEQUENCING_LIBRARIES_PATH"] ?? defaults.SequencingLibrariesPath,
            SequencingMappingTablePath =
                configuration["SEQUENCING_MAPPING_TABLE_PATH"] ?? defaults.SequencingMappingTablePath,
        };
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
