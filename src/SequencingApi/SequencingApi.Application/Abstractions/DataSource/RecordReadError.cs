namespace SequencingApi.Application.Abstractions.DataSource;

/// <summary>
/// A single record that could not be turned into a valid sequencing entity.
/// </summary>
/// <param name="Source">The data source that produced the record (see <see cref="ISequencingDataSource.Name"/>).</param>
/// <param name="Reference">Where the record came from — typically the file name or record id.</param>
/// <param name="Reason">Why it failed (the underlying validation/parse error description).</param>
public sealed record RecordReadError(string Source, string Reference, string Reason);
