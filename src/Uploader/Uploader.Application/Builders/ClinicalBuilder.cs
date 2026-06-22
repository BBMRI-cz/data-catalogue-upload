using System.Text.Json.Nodes;
using Uploader.Application.Json;
using Uploader.Domain;

namespace Uploader.Application.Builders;

/// <summary>Builds the patient-level FAIR Genomes value objects from a raw patient/sample payload.</summary>
public sealed class ClinicalBuilder
{
    public Personal BuildPersonal(JsonObject payload) => new()
    {
        PersonalIdentifier = payload.GetString("personal_identifier"),
        YearOfBirth = payload.GetInt("year_of_birth"),
        GenderAtBirth = payload.GetString("gender_at_birth"),
        GenderIdentity = payload.GetString("gender_identity"),
    };

    public Clinical BuildClinical(JsonObject payload) => new()
    {
        ClinicalIdentifier = payload.GetString("clinical_identifier"),
        BelongsToPerson = payload.GetString("belongs_to_person"),
        ClinicalDiagnosis = payload.GetStringList("clinical_diagnosis"),
        AgeAtDiagnosis = payload.GetInt("age_at_diagnosis"),
        AgeOfOnset = payload.GetInt("age_of_onset"),
    };

    public Material BuildMaterial(JsonObject payload) => new()
    {
        MaterialIdentifier = payload.GetString("material_identifier"),
        CollectedFromPerson = payload.GetString("collected_from_person"),
        BelongsToDiagnosis = payload.GetStringList("belongs_to_diagnosis"),
        SamplingTimestamp = payload.GetString("sampling_timestamp"),
        RegistrationTimestamp = payload.GetString("registration_timestamp"),
        SamplingProtocol = payload.GetString("sampling_protocol"),
        SamplingProtocolDeviation = payload.GetString("sampling_protocol_deviation"),
        ReasonForSamplingProtocolDeviation = payload.GetString("reason_for_sampling_protocol_deviation"),
        BiospecimenType = payload.GetString("biospecimen_type"),
        AnatomicalSource = payload.GetString("anatomical_source"),
        PathologicalState = payload.GetString("pathological_state"),
        StorageConditions = payload.GetString("storage_conditions"),
        ExpirationDate = payload.GetString("expiration_date"),
        PercentageTumorCells = payload.GetDouble("percentage_tumor_cells"),
        PhysicalLocation = payload.GetString("physical_location"),
        AnalysesPerformed = payload.GetStringList("analyses_performed"),
        DerivedFrom = payload.GetString("derived_from"),
    };
}
