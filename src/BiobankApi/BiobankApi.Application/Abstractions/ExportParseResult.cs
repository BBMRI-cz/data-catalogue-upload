using BiobankApi.Domain.Patients;

namespace BiobankApi.Application.Abstractions;

/// <summary>
/// The outcome of parsing one export source: the patients that validated successfully and the
/// records that failed. Invalid records are reported here, never silently dropped.
/// </summary>
public sealed record ExportParseResult(
    IReadOnlyList<PatientAggregate> Patients,
    IReadOnlyList<ExportParseError> Errors);
