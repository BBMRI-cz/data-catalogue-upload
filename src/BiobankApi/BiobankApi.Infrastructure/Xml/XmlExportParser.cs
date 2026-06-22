using System.Xml;
using BiobankApi.Application.Abstractions;
using BiobankApi.Domain.Patients;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Parses biobank XML exports into domain patients.
///
/// Scaffold: it discovers and validates that the export files are well-formed, but the concrete
/// element/XPath mapping to <see cref="Patient"/> / <see cref="Sample"/> lands with issue #33.
/// When implemented it will stream each file with <see cref="XmlReader"/> and build patients via
/// the <see cref="Domain.Services.IBiobankCleaningService"/>.
/// </summary>
public sealed class XmlExportParser(string exportPath) : IXmlExportSource
{
    public IReadOnlyList<Patient> ParsePatients()
    {
        var patients = new List<Patient>();
        if (!Directory.Exists(exportPath))
        {
            return patients;
        }

        var files = Directory.EnumerateFiles(exportPath, "*.xml").OrderBy(path => path, StringComparer.Ordinal);
        foreach (var file in files)
        {
            using var reader = XmlReader.Create(file);
            while (reader.Read())
            {
                // Read through to validate well-formedness; mapping to Patient/Sample is issue #33.
            }
        }

        return patients;
    }
}
