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
    public string SequencingDataPath { get; init; } = "data/records";

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
        };
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
