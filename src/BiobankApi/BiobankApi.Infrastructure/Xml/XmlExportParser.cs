using System.Xml;
using System.Xml.Linq;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;
using ErrorOr;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Parses a directory of biobank XML exports into domain patients. Each file holds exactly one
/// <c>&lt;patient&gt;</c>, so it is loaded whole with <see cref="XDocument"/> and mapped by
/// <see cref="XmlPatientReader"/>. A patient recurs in every weekly export, and each file lists only
/// the last ~60 days of samples, so a patient's files are merged by <see cref="PatientExportHistory"/>
/// into one aggregate rather than the newest file replacing the rest. Malformed files and records that
/// fail validation are reported as <see cref="ExportParseError"/>s rather than aborting the run.
/// </summary>
public sealed class XmlExportParser : IPatientExportSource
{
    private readonly string _exportPath;

    public XmlExportParser(string exportPath) => _exportPath = exportPath;

    public string Name => $"xml:{_exportPath}";

    public ErrorOr<ExportParseResult> ParsePatients()
    {
        if (!Directory.Exists(_exportPath))
        {
            return Error.Failure("Export.DirectoryMissing", $"export directory not found: {_exportPath}");
        }

        // Case-insensitive so uppercase `.XML` exports (as produced on the Linux server) are found.
        var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
        // Ordinal sort is intentional: filenames are `BBM{YYMMDD}{batch}-{seq}.XML`, so the fixed-width
        // YYMMDD makes ordinal order chronological, which is the order PatientExportHistory needs to
        // tell a patient's newest file from the older ones. (Assumes the 2-digit year stays this
        // century — true for the 2022-2026 dataset.)
        var files = Directory.EnumerateFiles(_exportPath, "*.xml", options)
            .OrderBy(path => path, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            return Error.Failure("Export.NoFiles", $"no XML exports found in: {_exportPath}");
        }

        // First-seen order, so the output follows the files the way it did when each was one patient.
        var histories = new OrderedDictionary<string, PatientExportHistory>(StringComparer.Ordinal);
        var errors = new List<ExportParseError>();

        foreach (var file in files)
        {
            var reference = Path.GetFileName(file);

            XElement? root;
            try
            {
                root = XDocument.Load(file).Root;
            }
            catch (XmlException exception)
            {
                errors.Add(new ExportParseError(Name, reference, exception.Message));
                continue;
            }

            if (root is null)
            {
                errors.Add(new ExportParseError(Name, reference, "empty document"));
                continue;
            }

            var parsed = XmlPatientReader.Read(root);
            if (parsed.IsError)
            {
                var reason = string.Join("; ", parsed.Errors.Select(error => error.Description));
                errors.Add(new ExportParseError(Name, reference, reason));
            }
            else if (histories.TryGetValue(parsed.Value.Id.Value, out var history))
            {
                history.Add(parsed.Value);
            }
            else
            {
                histories.Add(parsed.Value.Id.Value, new PatientExportHistory(parsed.Value));
            }
        }

        var patients = new List<PatientAggregate>(histories.Count);
        foreach (var (patientId, history) in histories)
        {
            // Every file already passed Create on its own, so a merge failure is a bug in the merge;
            // it is still reported per patient rather than aborting the ingest.
            var merged = history.Build();
            if (merged.IsError)
            {
                var reason = string.Join("; ", merged.Errors.Select(error => error.Description));
                errors.Add(new ExportParseError(Name, patientId, reason));
            }
            else
            {
                patients.Add(merged.Value);
            }
        }

        return new ExportParseResult(patients, errors);
    }
}
