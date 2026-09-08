using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Uploader.Infrastructure.Configuration;

/// <summary>
/// Runtime configuration for the uploader, read from environment variables (matching the Python
/// service's names). The DB connects to <c>localhost:POSTGRES_PORT</c>, as the host-run job does.
/// </summary>
public sealed class UploaderOptions
{
    public string PostgresUser { get; init; } = "postgres";
    public string PostgresPassword { get; init; } = "postgres";
    public string PostgresDb { get; init; } = "data_catalogue_upload";
    public string PostgresHost { get; init; } = "localhost";
    public int PostgresPort { get; init; } = 5432;

    public string BiobankApiUrl { get; init; } = "http://localhost:8001";
    public string RadiologyApiUrl { get; init; } = "http://localhost:8002";
    public string SequencingApiUrl { get; init; } = "http://localhost:8003";
    public string WsiApiUrl { get; init; } = "http://localhost:8004";
    public string CatalogueApiUrl { get; init; } = "http://localhost:8000";

    /// <summary>
    /// Biobank prefix on every pseudonym this uploader mints, as <c>&lt;prefix&gt;_&lt;kind&gt;_&lt;uuid&gt;</c>.
    /// Matches what the pseudonymizer already produces for MMCI; a second biobank is a second
    /// deployment with a different value.
    /// </summary>
    public string PseudonymPrefix { get; init; } = "mmci";

    public string ConnectionString =>
        $"Host={PostgresHost};Port={PostgresPort};Database={PostgresDb};Username={PostgresUser};Password={PostgresPassword}";

    public static UploaderOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new UploaderOptions();
        return new UploaderOptions
        {
            PostgresUser = configuration["POSTGRES_USER"] ?? defaults.PostgresUser,
            PostgresPassword = configuration["POSTGRES_PASSWORD"] ?? defaults.PostgresPassword,
            PostgresDb = configuration["POSTGRES_DB"] ?? defaults.PostgresDb,
            PostgresHost = configuration["POSTGRES_HOST"] ?? defaults.PostgresHost,
            PostgresPort = ParseInt(configuration["POSTGRES_PORT"], defaults.PostgresPort),
            BiobankApiUrl = configuration["BIOBANK_API_URL"] ?? defaults.BiobankApiUrl,
            RadiologyApiUrl = configuration["RADIOLOGY_API_URL"] ?? defaults.RadiologyApiUrl,
            SequencingApiUrl = configuration["SEQUENCING_API_URL"] ?? defaults.SequencingApiUrl,
            WsiApiUrl = configuration["WSI_API_URL"] ?? defaults.WsiApiUrl,
            CatalogueApiUrl = configuration["CATALOGUE_API_URL"] ?? defaults.CatalogueApiUrl,
            PseudonymPrefix = configuration["PSEUDONYM_PREFIX"] ?? defaults.PseudonymPrefix,
        };
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
