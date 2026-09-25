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
    public string SequencingApiUrl { get; init; } = "http://localhost:8002";

    /// <summary>
    /// Radiology and WSI have no service yet (#29, #31). They default to blank, which means "not
    /// deployed": no HTTP client is registered and nothing contacts them. Give one a URL and it
    /// joins the run. Blank is also why radiology no longer defaults to :8002, which is the
    /// sequencing API's port.
    /// </summary>
    public string RadiologyApiUrl { get; init; } = string.Empty;

    public string WsiApiUrl { get; init; } = string.Empty;

    public string CatalogueApiUrl { get; init; } = "http://localhost:8000";

    /// <summary>The EMX2 schema to write into, e.g. <c>FairGenomesTest</c>.</summary>
    public string CatalogueSchema { get; init; } = "FairGenomesTest";

    /// <summary>
    /// EMX2 API token, sent as <c>x-molgenis-token</c>. Blank by default and never defaulted to a
    /// real value: it comes from the environment, and <c>.env</c> is git-ignored.
    /// </summary>
    public string CatalogueToken { get; init; } = string.Empty;

    /// <summary>The one study every uploaded patient is published under.</summary>
    public string CatalogueStudyId { get; init; } = "mmci_biobank";

    public string CatalogueStudyName { get; init; } = "MMCI Biobank";

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
            CatalogueSchema = configuration["CATALOGUE_SCHEMA"] ?? defaults.CatalogueSchema,
            CatalogueToken = configuration["CATALOGUE_TOKEN"] ?? defaults.CatalogueToken,
            CatalogueStudyId = configuration["CATALOGUE_STUDY_ID"] ?? defaults.CatalogueStudyId,
            CatalogueStudyName = configuration["CATALOGUE_STUDY_NAME"] ?? defaults.CatalogueStudyName,
            PseudonymPrefix = configuration["PSEUDONYM_PREFIX"] ?? defaults.PseudonymPrefix,
        };
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
