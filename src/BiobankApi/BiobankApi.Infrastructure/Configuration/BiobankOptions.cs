using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace BiobankApi.Infrastructure.Configuration;

/// <summary>
/// Runtime configuration for the biobank API, read from environment variables (the same names
/// the Python service and the biobank-db container use). Defaults keep the app runnable without
/// a configured environment.
/// </summary>
public sealed class BiobankOptions
{
    public string PostgresUser { get; init; } = "postgres";
    public string PostgresPassword { get; init; } = "postgres";
    public string PostgresDb { get; init; } = "biobank_api";
    public string PostgresHost { get; init; } = "localhost";
    public int PostgresPort { get; init; } = 5433;
    public string BiobankHost { get; init; } = "0.0.0.0";
    public int BiobankPort { get; init; } = 8001;
    public string BiobankXmlExportPath { get; init; } = "data/exports";

    public string ConnectionString =>
        $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    public static BiobankOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new BiobankOptions();
        return new BiobankOptions
        {
            PostgresUser = configuration["POSTGRES_USER"] ?? defaults.PostgresUser,
            PostgresPassword = configuration["POSTGRES_PASSWORD"] ?? defaults.PostgresPassword,
            PostgresDb = configuration["POSTGRES_DB"] ?? defaults.PostgresDb,
            PostgresHost = configuration["POSTGRES_HOST"] ?? defaults.PostgresHost,
            PostgresPort = ParseInt(configuration["POSTGRES_PORT"], defaults.PostgresPort),
            BiobankHost = configuration["BIOBANK_HOST"] ?? defaults.BiobankHost,
            BiobankPort = ParseInt(configuration["BIOBANK_PORT"], defaults.BiobankPort),
            BiobankXmlExportPath = configuration["BIOBANK_XML_EXPORT_PATH"] ?? defaults.BiobankXmlExportPath,
        };
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
