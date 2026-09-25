namespace Uploader.Infrastructure.Http;

/// <summary>
/// The EMX2 tables the uploader writes, and the key column of each. Table ids are PascalCase and
/// key columns are camelCase, because that is how EMX2 spells them in a GraphQL mutation - see
/// <c>docs/catalogue-api-contract.md</c>.
/// <para>
/// Write order is the order they are declared in; delete order is the reverse. The catalogue
/// enforces it either way, since it refuses to delete a row another row still references.
/// </para>
/// </summary>
internal static class Emx2Tables
{
    public const string Study = "Study";
    public const string Personal = "Personal";
    public const string IndividualConsent = "IndividualConsent";
    public const string Clinical = "Clinical";
    public const string Material = "Material";
    public const string Biospecimens = "Biospecimens";
    public const string SamplePreparation = "SamplePreparation";
    public const string Sequencing = "Sequencing";
    public const string Analysis = "Analysis";

    public const string PersonalKey = "personalIdentifier";
    public const string ConsentKey = "individualConsentIdentifier";
    public const string ClinicalKey = "clinicalIdentifier";
    public const string MaterialKey = "materialIdentifier";
    public const string BiospecimenKey = "biospecimenIdentifier";
    public const string PreparationKey = "sampleprepIdentifier";
    public const string SequencingKey = "sequencingIdentifier";
    public const string AnalysisKey = "analysisIdentifier";
}
