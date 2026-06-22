using BiobankApi.Domain.Common;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence.Entities;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>Bidirectional mapping between domain patients and EF persistence rows.</summary>
internal static class PatientMapper
{
    // --- domain -> entity --------------------------------------------------------------

    public static PatientEntity ToEntity(Patient patient)
    {
        var entity = new PatientEntity
        {
            PatientId = patient.PatientId,
            Biobank = patient.Biobank,
            Consent = patient.Consent,
            Sex = patient.Sex,
            BirthYear = patient.BirthYear,
            BirthMonth = patient.BirthMonth,
            AccessionNumbers = [.. patient.AccessionNumbers],
        };

        foreach (var sample in patient.Samples)
        {
            switch (sample)
            {
                case TissueSample tissue:
                    entity.TissueSamples.Add(ToEntity(tissue, patient.PatientId));
                    break;
                case SerumSample serum:
                    entity.SerumSamples.Add(ToEntity(serum, patient.PatientId));
                    break;
                case GenomeSample genome:
                    entity.GenomeSamples.Add(ToEntity(genome, patient.PatientId));
                    break;
                default:
                    throw new DomainException($"unsupported sample type: {sample.GetType().Name}");
            }
        }

        foreach (var specimen in patient.DiagnosticSpecimens)
        {
            entity.DiagnosticSpecimens.Add(ToEntity(specimen, patient.PatientId));
        }

        return entity;
    }

    private static TissueSampleEntity ToEntity(TissueSample sample, string patientId) => new()
    {
        SampleId = sample.SampleId,
        PatientId = patientId,
        MaterialType = sample.MaterialType,
        EventNumber = sample.EventNumber,
        CollectionYear = sample.CollectionYear,
        Biopsy = sample.Biopsy,
        PredictiveNumber = sample.PredictiveNumber,
        SamplesNo = sample.SamplesNo,
        AvailableSamplesNo = sample.AvailableSamplesNo,
        AccessionNumbers = [.. sample.AccessionNumbers],
        Diagnosis = sample.Diagnosis,
        PTnm = sample.PTnm,
        Morphology = sample.Morphology,
        CutTime = sample.CutTime,
        FreezeTime = sample.FreezeTime,
        Retrieved = sample.Retrieved,
    };

    private static SerumSampleEntity ToEntity(SerumSample sample, string patientId) => new()
    {
        SampleId = sample.SampleId,
        PatientId = patientId,
        MaterialType = sample.MaterialType,
        EventNumber = sample.EventNumber,
        CollectionYear = sample.CollectionYear,
        Biopsy = sample.Biopsy,
        PredictiveNumber = sample.PredictiveNumber,
        SamplesNo = sample.SamplesNo,
        AvailableSamplesNo = sample.AvailableSamplesNo,
        AccessionNumbers = [.. sample.AccessionNumbers],
        Diagnosis = sample.Diagnosis,
        TakingDate = sample.TakingDate,
        Retrieved = sample.Retrieved,
    };

    private static GenomeSampleEntity ToEntity(GenomeSample sample, string patientId) => new()
    {
        SampleId = sample.SampleId,
        PatientId = patientId,
        MaterialType = sample.MaterialType,
        EventNumber = sample.EventNumber,
        CollectionYear = sample.CollectionYear,
        Biopsy = sample.Biopsy,
        PredictiveNumber = sample.PredictiveNumber,
        SamplesNo = sample.SamplesNo,
        AvailableSamplesNo = sample.AvailableSamplesNo,
        AccessionNumbers = [.. sample.AccessionNumbers],
        TakingDate = sample.TakingDate,
        Retrieved = sample.Retrieved,
    };

    private static DiagnosticSpecimenEntity ToEntity(DiagnosticSpecimen specimen, string patientId) => new()
    {
        SampleId = specimen.SampleId,
        PatientId = patientId,
        SpecimenNumber = specimen.SpecimenNumber,
        Year = specimen.Year,
        MaterialType = specimen.MaterialType,
        Diagnosis = specimen.Diagnosis,
        TakingDate = specimen.TakingDate,
        Retrieved = specimen.Retrieved,
    };

    // --- entity -> domain --------------------------------------------------------------

    public static Patient ToDomain(PatientEntity entity)
    {
        var samples = new List<Sample>();
        samples.AddRange(entity.TissueSamples.Select(ToDomain));
        samples.AddRange(entity.SerumSamples.Select(ToDomain));
        samples.AddRange(entity.GenomeSamples.Select(ToDomain));

        return new Patient(
            patientId: entity.PatientId,
            biobank: entity.Biobank,
            consent: entity.Consent,
            sex: entity.Sex,
            birthYear: entity.BirthYear,
            birthMonth: entity.BirthMonth,
            accessionNumbers: [.. entity.AccessionNumbers],
            samples: samples,
            diagnosticSpecimens: entity.DiagnosticSpecimens.Select(ToDomain).ToList());
    }

    private static TissueSample ToDomain(TissueSampleEntity row) => new(
        sampleId: row.SampleId,
        materialType: row.MaterialType,
        eventNumber: row.EventNumber,
        collectionYear: row.CollectionYear,
        biopsy: row.Biopsy,
        predictiveNumber: row.PredictiveNumber,
        samplesNo: row.SamplesNo,
        availableSamplesNo: row.AvailableSamplesNo,
        accessionNumbers: [.. row.AccessionNumbers],
        diagnosis: row.Diagnosis,
        pTnm: row.PTnm,
        morphology: row.Morphology,
        cutTime: row.CutTime,
        freezeTime: row.FreezeTime,
        retrieved: row.Retrieved);

    private static SerumSample ToDomain(SerumSampleEntity row) => new(
        sampleId: row.SampleId,
        materialType: row.MaterialType,
        eventNumber: row.EventNumber,
        collectionYear: row.CollectionYear,
        biopsy: row.Biopsy,
        predictiveNumber: row.PredictiveNumber,
        samplesNo: row.SamplesNo,
        availableSamplesNo: row.AvailableSamplesNo,
        accessionNumbers: [.. row.AccessionNumbers],
        diagnosis: row.Diagnosis,
        takingDate: row.TakingDate,
        retrieved: row.Retrieved);

    private static GenomeSample ToDomain(GenomeSampleEntity row) => new(
        sampleId: row.SampleId,
        materialType: row.MaterialType,
        eventNumber: row.EventNumber,
        collectionYear: row.CollectionYear,
        biopsy: row.Biopsy,
        predictiveNumber: row.PredictiveNumber,
        samplesNo: row.SamplesNo,
        availableSamplesNo: row.AvailableSamplesNo,
        accessionNumbers: [.. row.AccessionNumbers],
        takingDate: row.TakingDate,
        retrieved: row.Retrieved);

    private static DiagnosticSpecimen ToDomain(DiagnosticSpecimenEntity row) => new(
        sampleId: row.SampleId,
        specimenNumber: row.SpecimenNumber,
        year: row.Year,
        materialType: row.MaterialType,
        diagnosis: row.Diagnosis,
        takingDate: row.TakingDate,
        retrieved: row.Retrieved);
}
