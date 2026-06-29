using System.Xml;
using System.Xml.Linq;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain.Patients;

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

    public ExportParseResult ParsePatients()
    {
        var patients = new List<PatientAggregate>();
        var errors = new List<ExportParseError>();

        if (!Directory.Exists(_exportPath))
        {
            return new ExportParseResult(patients, errors);
        }

        var files = Directory.EnumerateFiles(_exportPath, "*.xml").OrderBy(path => path, StringComparer.Ordinal);
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
