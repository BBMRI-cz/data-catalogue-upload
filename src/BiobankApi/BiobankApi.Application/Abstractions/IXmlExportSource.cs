using BiobankApi.Domain.Patients;

namespace BiobankApi.Application.Abstractions;

/// <summary>
/// Source port that parses biobank XML exports into domain patients (implemented by the xml layer).
/// </summary>
public interface IXmlExportSource
{
    /// <summary>Parse all configured XML exports into domain patients.</summary>
    IReadOnlyList<Patient> ParsePatients();
}
