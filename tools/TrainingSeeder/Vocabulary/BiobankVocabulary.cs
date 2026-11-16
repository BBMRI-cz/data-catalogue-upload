namespace TrainingSeeder.Vocabulary;

/// <summary>
/// <c>biobank-vocabulary.json</c>: what the real biobank database holds, as counts only. Each section
/// is keyed <c>table.column</c>; see <c>export-vocabulary.sql</c> for how every number is computed.
/// </summary>
/// <param name="Schema">The last migration the exported database had applied.</param>
/// <param name="Patients">The real patient count, the default dataset size.</param>
/// <param name="Values">Every value of a coded column with its count, null included.</param>
/// <param name="PerPatient">How many patients have k rows of a kind (<c>tissue_sample</c> and so on).</param>
/// <param name="Ranges">Min, median and max of the aliquot counts.</param>
/// <param name="Filled">How often an identifier or date column is filled in.</param>
internal sealed record BiobankVocabulary(
    string Source,
    string Schema,
    int Patients,
    IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> Values,
    IReadOnlyDictionary<string, IReadOnlyList<ValueCount>> PerPatient,
    IReadOnlyDictionary<string, NumberRange> Ranges,
    IReadOnlyDictionary<string, FilledCount> Filled);
