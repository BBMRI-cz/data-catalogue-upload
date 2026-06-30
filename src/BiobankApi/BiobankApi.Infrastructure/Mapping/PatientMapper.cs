using BiobankApi.Domain.Common;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence.Entities;

namespace BiobankApi.Infrastructure.Mapping;

/// <summary>
/// Maps between domain patients and EF persistence rows. The patient assembly is hand-written because
/// the domain holds its samples in one polymorphic <c>IReadOnlyList&lt;Sample&gt;</c> that fans out to
/// three entity tables (and the <c>PatientId</c> foreign key has no domain counterpart).
/// </summary>
internal static class PatientMapper
{
    // --- domain -> entity --------------------------------------------------------------

    public static PatientEntity ToEntity(PatientAggregate patient)
    {
        var patientId = patient.Id.Value;

        var entity = new PatientEntity
        {
            PatientId = patientId,
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
                    entity.TissueSamples.Add(WithPatientId(ToEntity(tissue), patientId));
                    break;
                case SerumSample serum:
                    entity.SerumSamples.Add(WithPatientId(ToEntity(serum), patientId));
                    break;
                case GenomeSample genome:
                    entity.GenomeSamples.Add(WithPatientId(ToEntity(genome), patientId));
                    break;
                default:
                    // Unreachable: Sample is sealed-by-subtype; a new subtype is a programmer error.
                    throw new InvalidOperationException($"unsupported sample type: {sample.GetType().Name}");
            }
        }

        foreach (var specimen in patient.DiagnosticSpecimens)
        {
            var row = ToEntity(specimen);
            row.PatientId = patientId;
            entity.DiagnosticSpecimens.Add(row);
        }

        return entity;
    }

    // Id (surrogate) and PatientId (FK) are set by the caller, not copied from the domain sample.
    private static TissueSampleEntity ToEntity(TissueSample sample) => new()
    {
        SampleId = sample.Id.Value,
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

    private static SerumSampleEntity ToEntity(SerumSample sample) => new()
    {
        SampleId = sample.Id.Value,
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

    private static GenomeSampleEntity ToEntity(GenomeSample sample) => new()
    {
        SampleId = sample.Id.Value,
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

    private static DiagnosticSpecimenEntity ToEntity(DiagnosticSpecimen specimen) => new()
    {
        SampleId = specimen.Id.Value,
        SpecimenNumber = specimen.SpecimenNumber,
        Year = specimen.Year,
        MaterialType = specimen.MaterialType,
        Diagnosis = specimen.Diagnosis,
        TakingDate = specimen.TakingDate,
        Retrieved = specimen.Retrieved,
    };

    private static TEntity WithPatientId<TEntity>(TEntity row, string patientId)
        where TEntity : SampleEntityBase
    {
        row.PatientId = patientId;
        return row;
    }

    // --- entity -> domain --------------------------------------------------------------

    public static PatientAggregate ToDomain(PatientEntity entity)
    {
        var samples = new List<Sample>(
            entity.TissueSamples.Count + entity.SerumSamples.Count + entity.GenomeSamples.Count);
        samples.AddRange(entity.TissueSamples.Select(ToDomain));
        samples.AddRange(entity.SerumSamples.Select(ToDomain));
        samples.AddRange(entity.GenomeSamples.Select(ToDomain));

        // Reconstitution from trusted persistence intentionally bypasses PatientAggregate.Create:
        // the row was validated on the way in, so we rebuild via the (internal) initializer.
        return new PatientAggregate
        {
            Id = new PatientId(entity.PatientId),
            Biobank = entity.Biobank,
            Consent = entity.Consent,
            Sex = entity.Sex,
            BirthYear = entity.BirthYear,
            BirthMonth = entity.BirthMonth,
            AccessionNumbers = [.. entity.AccessionNumbers],
            Samples = samples,
            DiagnosticSpecimens = [.. entity.DiagnosticSpecimens.Select(ToDomain)],
        };
    }

    // The surrogate Id column has no domain counterpart; SampleId carries the business id.
    private static TissueSample ToDomain(TissueSampleEntity row) => new()
    {
        Id = new SampleId(row.SampleId),
        MaterialType = row.MaterialType,
        EventNumber = row.EventNumber,
        CollectionYear = row.CollectionYear,
        Biopsy = row.Biopsy,
        PredictiveNumber = row.PredictiveNumber,
        SamplesNo = row.SamplesNo,
        AvailableSamplesNo = row.AvailableSamplesNo,
        AccessionNumbers = [.. row.AccessionNumbers],
        Diagnosis = row.Diagnosis,
        PTnm = row.PTnm,
        Morphology = row.Morphology,
        CutTime = row.CutTime,
        FreezeTime = row.FreezeTime,
        Retrieved = row.Retrieved,
    };

    private static SerumSample ToDomain(SerumSampleEntity row) => new()
    {
        Id = new SampleId(row.SampleId),
        MaterialType = row.MaterialType,
        EventNumber = row.EventNumber,
        CollectionYear = row.CollectionYear,
        Biopsy = row.Biopsy,
        PredictiveNumber = row.PredictiveNumber,
        SamplesNo = row.SamplesNo,
        AvailableSamplesNo = row.AvailableSamplesNo,
        AccessionNumbers = [.. row.AccessionNumbers],
        Diagnosis = row.Diagnosis,
        TakingDate = row.TakingDate,
        Retrieved = row.Retrieved,
    };

    private static GenomeSample ToDomain(GenomeSampleEntity row) => new()
    {
        Id = new SampleId(row.SampleId),
        MaterialType = row.MaterialType,
        EventNumber = row.EventNumber,
        CollectionYear = row.CollectionYear,
        Biopsy = row.Biopsy,
        PredictiveNumber = row.PredictiveNumber,
        SamplesNo = row.SamplesNo,
        AvailableSamplesNo = row.AvailableSamplesNo,
        AccessionNumbers = [.. row.AccessionNumbers],
        TakingDate = row.TakingDate,
        Retrieved = row.Retrieved,
    };

    private static DiagnosticSpecimen ToDomain(DiagnosticSpecimenEntity row) => new()
    {
        Id = new SpecimenId(row.SampleId),
        SpecimenNumber = row.SpecimenNumber,
        Year = row.Year,
        MaterialType = row.MaterialType,
        Diagnosis = row.Diagnosis,
        TakingDate = row.TakingDate,
        Retrieved = row.Retrieved,
    };
}
