using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Domain;

namespace Uploader.Application.Mapping;

/// <summary>Maps the raw biobank patient DTO onto the <see cref="PatientAggregate"/>.</summary>
public static class PatientMapper
{
    public static ErrorOr<PatientAggregate> ToPatient(PatientDto dto) =>
        PatientAggregate.Create(dto.PatientId, ToPersonal(dto), ToClinical(dto));

    private static Personal ToPersonal(PatientDto dto) => new()
    {
        PersonalIdentifier = dto.PersonalIdentifier,
        YearOfBirth = dto.YearOfBirth,
        GenderAtBirth = dto.GenderAtBirth,
        GenderIdentity = dto.GenderIdentity,
    };

    private static Clinical ToClinical(PatientDto dto) => new()
    {
        ClinicalIdentifier = dto.ClinicalIdentifier,
        BelongsToPerson = dto.BelongsToPerson,
        ClinicalDiagnosis = dto.ClinicalDiagnosis,
        AgeAtDiagnosis = dto.AgeAtDiagnosis,
        AgeOfOnset = dto.AgeOfOnset,
    };
}
