namespace BiobankApi.Application.Abstractions;

/// <summary>
/// Source port that turns a biobank's raw export into domain patients. XML is one implementation
/// (<c>XmlExportParser</c>); a biobank with no XML export contributes a different source (or none).
/// The ingestion use case aggregates every registered source, so this stays format-agnostic.
/// </summary>
public interface IPatientExportSource
{
    /// <summary>A short label identifying the source, used when reporting parse failures.</summary>
    string Name { get; }

    /// <summary>Parse the source into domain patients, reporting per-record failures alongside them.</summary>
    ExportParseResult ParsePatients();
}
