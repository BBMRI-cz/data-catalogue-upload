namespace BiobankApi.Application.Abstractions.Export;

/// <summary>
/// A single record that could not be turned into a valid patient.
/// </summary>
/// <param name="Source">The export source that produced the record (see <see cref="IPatientExportSource.Name"/>).</param>
/// <param name="Reference">Where the record came from — typically the file name or patient id.</param>
/// <param name="Reason">Why it failed (the underlying validation/parse error description).</param>
public sealed record ExportParseError(string Source, string Reference, string Reason);
