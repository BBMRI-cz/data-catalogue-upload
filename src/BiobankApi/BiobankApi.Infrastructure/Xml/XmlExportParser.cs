using System.Xml;
using System.Xml.Linq;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;
using ErrorOr;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Parses a directory of biobank XML exports into domain patients. Each file holds exactly one
/// <c>&lt;patient&gt;</c>, so it is loaded whole with <see cref="XDocument"/> and mapped by
/// <see cref="XmlPatientReader"/>. Malformed files and records that fail validation are reported as
/// <see cref="ExportParseError"/>s rather than aborting the run.
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

        var files = Directory.EnumerateFiles(_exportPath, "*.xml").OrderBy(path => path, StringComparer.Ordinal).ToList();
        if (files.Count == 0)
        {
            return Error.Failure("Export.NoFiles", $"no XML exports found in: {_exportPath}");
        }

        var patients = new List<PatientAggregate>();
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
            else
            {
                patients.Add(parsed.Value);
            }
        }

        return new ExportParseResult(patients, errors);
    }
}
