using BiobankApi.Domain.Common;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence.Entities;
using Riok.Mapperly.Abstractions;

namespace BiobankApi.Infrastructure.Persistence;

/// <summary>
/// Maps between domain patients and EF persistence rows. Mapperly generates the tedious
/// per-sample-type field copies; the patient assembly is hand-written because the domain holds its
/// samples in one polymorphic <c>IReadOnlyList&lt;Sample&gt;</c> that fans out to three entity
/// tables (and the <c>PatientId</c> foreign key has no domain counterpart).
/// </summary>
[Mapper]
internal static partial class PatientMapper
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

    [MapProperty(nameof(TissueSample.Id), nameof(TissueSampleEntity.SampleId))]
    [MapperIgnoreTarget(nameof(TissueSampleEntity.PatientId))]
    private static partial TissueSampleEntity ToEntity(TissueSample sample);

    [MapProperty(nameof(SerumSample.Id), nameof(SerumSampleEntity.SampleId))]
    [MapperIgnoreTarget(nameof(SerumSampleEntity.PatientId))]
    private static partial SerumSampleEntity ToEntity(SerumSample sample);

    [MapProperty(nameof(GenomeSample.Id), nameof(GenomeSampleEntity.SampleId))]
    [MapperIgnoreTarget(nameof(GenomeSampleEntity.PatientId))]
    private static partial GenomeSampleEntity ToEntity(GenomeSample sample);

    [MapProperty(nameof(DiagnosticSpecimen.Id), nameof(DiagnosticSpecimenEntity.SampleId))]
    [MapperIgnoreTarget(nameof(DiagnosticSpecimenEntity.PatientId))]
    private static partial DiagnosticSpecimenEntity ToEntity(DiagnosticSpecimen specimen);

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

    [MapProperty(nameof(TissueSampleEntity.SampleId), nameof(TissueSample.Id))]
    private static partial TissueSample ToDomain(TissueSampleEntity row);

    [MapProperty(nameof(SerumSampleEntity.SampleId), nameof(SerumSample.Id))]
    private static partial SerumSample ToDomain(SerumSampleEntity row);

    [MapProperty(nameof(GenomeSampleEntity.SampleId), nameof(GenomeSample.Id))]
    private static partial GenomeSample ToDomain(GenomeSampleEntity row);

    [MapProperty(nameof(DiagnosticSpecimenEntity.SampleId), nameof(DiagnosticSpecimen.Id))]
    private static partial DiagnosticSpecimen ToDomain(DiagnosticSpecimenEntity row);

    // --- strongly-typed id <-> string converters (picked up by Mapperly) ---------------

    private static string FromId(SampleId id) => id.Value;

    private static string FromId(SpecimenId id) => id.Value;

    private static SampleId ToSampleId(string value) => new(value);

    private static SpecimenId ToSpecimenId(string value) => new(value);
}
