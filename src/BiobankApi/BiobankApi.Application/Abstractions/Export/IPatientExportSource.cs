using ErrorOr;

namespace BiobankApi.Application.Abstractions.Export;

/// <summary>
/// Source port that turns a biobank's raw export into domain patients. XML is one implementation
/// (<c>XmlExportParser</c>); a biobank with no XML export contributes a different source. There is
/// exactly one source per biobank, so this stays format-agnostic.
/// </summary>
public interface IPatientExportSource
{
    /// <summary>A short label identifying the source, used when reporting parse failures.</summary>
    string Name { get; }

    /// <summary>
    /// Parse the source into domain patients, reporting per-record failures alongside them. Returns an
    /// error when the source itself is unavailable (e.g. the export directory is missing or empty), so
    /// a misconfigured run fails loudly instead of looking like a successful ingest of nothing.
    /// </summary>
    ErrorOr<ExportParseResult> ParsePatients();
}
